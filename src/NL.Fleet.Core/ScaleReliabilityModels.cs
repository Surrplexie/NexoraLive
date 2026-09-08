namespace NL.Fleet.Core;

public sealed record ScaleReliabilityValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record ScaleReliabilityValidationReport(
    bool ScaleReliabilityPassed,
    IReadOnlyList<ScaleReliabilityValidationCheck> Checks,
    IReadOnlyList<FleetSloStatus> ProductionSlos,
    DateTimeOffset EvaluatedAtUtc);

public sealed record ScaleReliabilityStatus(
    bool Enabled,
    bool DevMode,
    int MinConcurrentSessions,
    int RegionCount,
    int MaxConcurrentSessions,
    bool LoadTestRecorded,
    int? LastLoadTestTarget,
    bool DistributionEnabled,
    DateTimeOffset ObservedAtUtc);
