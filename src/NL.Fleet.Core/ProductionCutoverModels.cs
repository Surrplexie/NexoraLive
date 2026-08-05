namespace NL.Fleet.Core;

public sealed record ProductionCutoverValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record ProductionCutoverValidationReport(
    bool ProductionCutoverPassed,
    IReadOnlyList<ProductionCutoverValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);

public sealed record ProductionCutoverStatus(
    bool Enabled,
    bool DevMode,
    bool LiveProductionDevDisabled,
    bool LaunchOpsDevDisabled,
    bool MockIdentityDisabled,
    bool GaRequireProductionReady,
    bool GaRequireLiveIdentity,
    string? PublicBaseUrl,
    bool HardeningEnabled,
    DateTimeOffset ObservedAtUtc);
