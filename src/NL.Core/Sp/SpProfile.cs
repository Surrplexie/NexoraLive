namespace NL.Core.Sp;

/// <summary>
/// A StreamPlayer (SP) — nl.txt section 2: "a player/fan/supporter as a viewer from a
/// streaming platform, the player doesn't need to know the streamer to join their games if
/// they are a SP." One profile is shared across every streamer the SP interacts with; only
/// <see cref="SpStreamerRelationship"/> (standing, roles, follow/sub status) is per-streamer.
/// </summary>
public sealed class SpProfile
{
    private readonly Dictionary<string, SpStreamerRelationship> _relationships = new();

    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public DateTimeOffset AccountCreatedAtUtc { get; init; }

    public SpVerification Verification { get; init; } = SpVerification.None;

    public List<SpOffense> Offenses { get; init; } = new();

    /// <summary>Relationship with a given streamer, or a fresh default (Normal standing, no
    /// roles, no follow/sub) if the SP has never interacted with that streamer before.</summary>
    public SpStreamerRelationship GetRelationship(string streamerId) =>
        _relationships.TryGetValue(streamerId, out var relationship)
            ? relationship
            : new SpStreamerRelationship(streamerId);

    public void SetRelationship(SpStreamerRelationship relationship) =>
        _relationships[relationship.StreamerId] = relationship;

    /// <summary>Every streamer relationship this SP currently has, keyed by streamer id.
    /// Read-only view — added for Phase 4 persistence (a repository needs to serialize every
    /// relationship, not just look one up); mutate via <see cref="SetRelationship"/>.</summary>
    public IReadOnlyDictionary<string, SpStreamerRelationship> Relationships => _relationships;

    /// <summary>Active (non-archived) offenses this SP has with one specific streamer.</summary>
    public int ActiveOffenseCount(string streamerId, DateTimeOffset nowUtc) =>
        Offenses.Count(o => o.StreamerId == streamerId && o.IsActive(nowUtc));

    /// <summary>Active offenses across every streamer — nl.txt's "just on a passive offenses
    /// list" implies the 2-year window is global bookkeeping, not per-streamer.</summary>
    public int ActiveOffenseCountAllStreamers(DateTimeOffset nowUtc) =>
        Offenses.Count(o => o.IsActive(nowUtc));

    public double AccountAgeDays(DateTimeOffset nowUtc) => (nowUtc - AccountCreatedAtUtc).TotalDays;
}
