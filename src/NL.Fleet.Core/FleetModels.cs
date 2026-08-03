namespace NL.Fleet.Core;

public sealed record FleetRegion(
    string Id,
    string DisplayName,
    int LatencyBiasMs = 0);

public sealed record FleetPlacementRequest(
    string StreamerId,
    string? PreferredRegion = null,
    string? StreamerGeoHint = null);

public sealed record FleetPlacementResult(
    string RegionId,
    string OrchestratorEndpoint,
    string RelayBaseUrl,
    bool UsedPreferredRegion);

public sealed record FleetRelayConfig(
    string RelayWebSocketTemplate,
    string TurnUri,
    bool MaskRawHostIps = true);

public sealed record FleetRelayConnectInfo(
    string PublicConnectUrl,
    string? RawEndpoint,
    string RelayRegion);

public sealed record FleetSessionMetricSample(
    string SessionId,
    string StreamerId,
    string RegionId,
    int DecisionCount,
    int AdmitDenials,
    bool ForkHealthy,
    DateTimeOffset SampledAtUtc);

public sealed record FleetObservabilitySnapshot(
    int ActiveForkSessions,
    int ActiveNlsSessions,
    int TotalAdmits,
    int TotalAdmitDenials,
    int TotalDecisions,
    int ForkCreateRateLastMinute,
    IReadOnlyList<FleetSessionMetricSample> RecentSessions,
    DateTimeOffset GeneratedAtUtc);

public sealed record FleetAutoscalePolicy(
    int MinWarmSnapshots,
    int MaxConcurrentSessions,
    bool ScaleToZeroWhenIdle,
    int IdleMinutesBeforeScaleDown);

public sealed record FleetWarmPoolState(
    int TargetWarm,
    int CurrentWarm,
    int ActiveSessions,
    bool ScaleToZeroEligible,
    DateTimeOffset UpdatedAtUtc);

public enum FleetIncidentKind
{
    ForkCrash,
    ForkUnhealthy,
    AdmitStorm,
    RegionDegraded,
}

public sealed record FleetIncident(
    string IncidentId,
    FleetIncidentKind Kind,
    string SessionId,
    string StreamerId,
    string Message,
    DateTimeOffset DetectedAtUtc,
    bool AutoRestartAttempted,
    string? SpectatorMessage = null);

public sealed record FleetAbusePolicy(
    int MaxForkCreatesPerStreamerPerHour,
    int MinTwitchFollowers,
    int GlobalForkCreatesPerMinute);

public sealed record FleetAbuseCheckResult(
    bool Allowed,
    string? DenyReason = null);

public sealed record FleetStreamerRequirements(
    string StreamerId,
    int MinTwitchFollowers,
    int MinYouTubeSubscribers,
    bool EnforceOnForkCreate);

public sealed record FleetComplianceExport(
    string PlayerId,
    string ExportJson,
    DateTimeOffset ExportedAtUtc);

public sealed record FleetModerationRetentionPolicy(
    int RetentionDays,
    bool AllowGdprExport,
    bool AllowGdprDelete);

public sealed record FleetSloDefinition(
    string Name,
    double Target,
    string Unit,
    string Description);

public sealed record FleetSloStatus(
    string Name,
    double Target,
    double Current,
    bool Met,
    string Unit);

public sealed record FleetLoadTestResult(
    int ConcurrentSessionsTarget,
    int AdmitsPerSecondTarget,
    int AdmitsSucceeded,
    int AdmitsFailed,
    double ElapsedSeconds,
    double ForkCreateP99Ms,
    IReadOnlyList<FleetSloStatus> Slos);

public sealed record FleetValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record FleetValidationReport(
    bool ProductionReady,
    bool StagingPassed,
    IReadOnlyList<FleetValidationCheck> Checks,
    IReadOnlyList<FleetSloStatus> Slos,
    FleetLoadTestResult? LastLoadTest,
    DateTimeOffset EvaluatedAtUtc);
