using NL.Core;
using NL.Core.Sp;

namespace NL.Server.Core;

/// <summary>
/// Resolves SP id as the player's display name (prototype identity mapping), loads/creates
/// their <see cref="SpProfile"/> via a callback, and runs
/// <see cref="JoinEligibilityEngine"/>. Ban/graylist/requirements from Phase 2 + Phase 4
/// moderation therefore take effect on the next real join.
/// </summary>
public sealed class SpJoinGate : IJoinGate
{
    private readonly string _streamerId;
    private readonly JoinRequirements _requirements;
    private readonly Func<string, string, SpProfile> _getOrCreateProfile;
    private readonly Func<DateTimeOffset> _clock;

    public SpJoinGate(
        string streamerId,
        JoinRequirements requirements,
        Func<string, string, SpProfile> getOrCreateProfile,
        Func<DateTimeOffset>? clock = null)
    {
        _streamerId = streamerId;
        _requirements = requirements;
        _getOrCreateProfile = getOrCreateProfile;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public JoinGateOutcome? TryEvaluate(SessionEvent sessionEvent)
    {
        if (!string.Equals(sessionEvent.Event.Name, "playerJoin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var playerName = sessionEvent.PlayerName;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return null;
        }

        var playerId = playerName;
        var profile = _getOrCreateProfile(playerId, playerName);
        var join = JoinEligibilityEngine.Evaluate(profile, _streamerId, _requirements, _clock());

        var action = join.Decision == JoinDecision.Allow
            ? ActionResult.Allow(join.Reason)
            : ActionResult.Block(join.Reason ?? join.Decision.ToString());

        return new JoinGateOutcome(action, join, playerId);
    }
}
