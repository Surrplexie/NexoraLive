using NL.Core.Sp;

namespace NL.Moderation.Core;

/// <summary>A SP's standing + offense history with one streamer — what the "SP Offense
/// History" admin/mod view (ROADMAP.md Phase 4) needs to render.</summary>
public sealed record OffenseHistory(
    string StreamerId,
    SpStanding Standing,
    int ActiveOffenseCount,
    IReadOnlyList<SpOffense> Offenses);
