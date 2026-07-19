using NL.Core;
using NL.Core.Sp;

namespace NL.Moderation.Core;

/// <summary>
/// The Phase 4 façade: persists automatic `RuleEngine`/`NlServerHost` decisions as an audit
/// trail, and lets a mod/admin issue real actions (warning/ban/graylist/clear) that update the
/// Phase 2 SP model (<see cref="SpProfile"/>) and are themselves logged. Consuming code (a CLI,
/// a WinForms dashboard, or `NL.Server`) only ever talks to this class, never the stores
/// directly — mirrors how `JoinEligibilityEngine` is the one entry point for Phase 2.
/// </summary>
public sealed class ModerationService
{
    private readonly IModerationStore _store;
    private readonly ISpProfileRepository _profiles;
    private readonly Func<DateTimeOffset> _clock;

    public ModerationService(
        IModerationStore store, ISpProfileRepository profiles, Func<DateTimeOffset>? clock = null)
    {
        _store = store;
        _profiles = profiles;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Logs an automatic decision from the rule engine as an audit-trail entry —
    /// no SP standing change. <paramref name="playerId"/> is optional since Phase 3 sources
    /// (e.g. a Minecraft log) only carry a display name, not a resolved SP id.</summary>
    public Task RecordAutomaticDecisionAsync(
        string streamerId,
        string? playerName,
        GameEvent gameEvent,
        ActionResult result,
        string source,
        string? playerId = null,
        CancellationToken cancellationToken = default)
    {
        var record = ModerationRecord.ForAutomaticDecision(
            streamerId, playerName, gameEvent.Name, result.Decision, result.Message, source, _clock(), playerId);
        return _store.AppendAsync(record, cancellationToken);
    }

    /// <summary>Issues a warning: adds a <see cref="SpOffense"/> to the SP's history (no
    /// standing change) and logs the action.</summary>
    public async Task<SpOffense> IssueWarningAsync(
        string streamerId, string playerId, string issuedBy, string reason,
        string? game = null, CancellationToken cancellationToken = default)
    {
        var profile = GetProfileOrThrow(playerId);
        var offense = new SpOffense(streamerId, _clock(), issuedBy, reason, game);
        profile.Offenses.Add(offense);
        _profiles.Save(profile);

        await _store.AppendAsync(
            ModerationRecord.ForModAction(
                streamerId, ModerationActionKind.Warning, playerId, profile.DisplayName, reason, issuedBy, _clock()),
            cancellationToken);

        return offense;
    }

    /// <summary>Issues a ban: adds a <see cref="SpOffense"/>, sets standing to
    /// <see cref="SpStanding.Banned"/> for this streamer, and logs the action. A banned SP is
    /// immediately denied by <c>JoinEligibilityEngine</c> the next time they try to join.</summary>
    public async Task<SpOffense> IssueBanAsync(
        string streamerId, string playerId, string issuedBy, string reason,
        string? game = null, CancellationToken cancellationToken = default)
    {
        var profile = GetProfileOrThrow(playerId);
        var offense = new SpOffense(streamerId, _clock(), issuedBy, reason, game);
        profile.Offenses.Add(offense);
        SetStanding(profile, streamerId, SpStanding.Banned);
        _profiles.Save(profile);

        await _store.AppendAsync(
            ModerationRecord.ForModAction(
                streamerId, ModerationActionKind.Ban, playerId, profile.DisplayName, reason, issuedBy, _clock()),
            cancellationToken);

        return offense;
    }

    /// <summary>Puts a SP under investigation (nl.txt: "accidentally, false ban, etc.") — sets
    /// standing to <see cref="SpStanding.Graylist"/>, no offense recorded, and logs the
    /// action. <c>JoinEligibilityEngine</c> will Hold (or Deny, per that streamer's
    /// <c>JoinRequirements</c>) future join attempts.</summary>
    public async Task IssueGraylistHoldAsync(
        string streamerId, string playerId, string issuedBy, string reason,
        CancellationToken cancellationToken = default)
    {
        var profile = GetProfileOrThrow(playerId);
        SetStanding(profile, streamerId, SpStanding.Graylist);
        _profiles.Save(profile);

        await _store.AppendAsync(
            ModerationRecord.ForModAction(
                streamerId, ModerationActionKind.GraylistHold, playerId, profile.DisplayName, reason, issuedBy, _clock()),
            cancellationToken);
    }

    /// <summary>Clears a SP back to <see cref="SpStanding.Normal"/> standing for this
    /// streamer (investigation resolved, ban lifted, etc.) and logs the action. Does not
    /// remove any past <see cref="SpOffense"/> — offenses stay on the record per nl.txt's
    /// 2-year archive rule; only standing changes.</summary>
    public async Task ClearStandingAsync(
        string streamerId, string playerId, string issuedBy, string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var profile = GetProfileOrThrow(playerId);
        SetStanding(profile, streamerId, SpStanding.Normal);
        _profiles.Save(profile);

        await _store.AppendAsync(
            ModerationRecord.ForModAction(
                streamerId, ModerationActionKind.StandingCleared, playerId, profile.DisplayName,
                reason ?? "Standing cleared", issuedBy, _clock()),
            cancellationToken);
    }

    public Task<IReadOnlyList<ModerationRecord>> GetRecentActionsAsync(
        string streamerId, int count = 50, CancellationToken cancellationToken = default) =>
        _store.GetRecentAsync(streamerId, count, cancellationToken);

    public Task<IReadOnlyList<ModerationRecord>> GetPlayerActionsAsync(
        string streamerId, string playerId, CancellationToken cancellationToken = default) =>
        _store.GetForPlayerAsync(streamerId, playerId, cancellationToken);

    /// <summary>Standing + offense history for one SP with one streamer — what the "SP
    /// Offense History" view needs. Returns null if the SP is unknown.</summary>
    public OffenseHistory? GetOffenseHistory(string streamerId, string playerId, DateTimeOffset? nowUtc = null)
    {
        var profile = _profiles.Find(playerId);
        if (profile is null)
        {
            return null;
        }

        var now = nowUtc ?? _clock();
        var offenses = profile.Offenses
            .Where(o => o.StreamerId == streamerId)
            .OrderByDescending(o => o.IssuedAtUtc)
            .ToList();

        return new OffenseHistory(
            streamerId,
            profile.GetRelationship(streamerId).Standing,
            profile.ActiveOffenseCount(streamerId, now),
            offenses);
    }

    public SpProfile GetOrCreateProfile(string playerId, string displayName) =>
        _profiles.GetOrCreate(playerId, displayName);

    public IReadOnlyList<SpProfile> AllProfiles() => _profiles.All();

    private SpProfile GetProfileOrThrow(string playerId) =>
        _profiles.Find(playerId)
            ?? throw new InvalidOperationException($"Unknown SP '{playerId}' — create the profile first.");

    private static void SetStanding(SpProfile profile, string streamerId, SpStanding standing) =>
        profile.SetRelationship(profile.GetRelationship(streamerId) with { Standing = standing });
}
