namespace NL.AntiCheat.Core;

/// <summary>Tunable thresholds for the built-in Phase 5 detectors. Defaults are intentionally
/// aggressive for the checked-in sample (so a replay demo fires clearly); raise them for a
/// real session.</summary>
public sealed record AnomalyThresholds(
    int RateSpikeMaxEvents = 8,
    int RateSpikeWindowMs = 1000,
    double TeleportMaxDistance = 40.0,
    double TeleportMaxSpeedUnitsPerSec = 25.0)
{
    public static AnomalyThresholds Default { get; } = new();
}
