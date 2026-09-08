namespace NL.Fleet.Core;

public sealed record ProductionDogfoodValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record ProductionDogfoodValidationReport(
    bool ProductionDogfoodPassed,
    IReadOnlyList<ProductionDogfoodValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);

public sealed record ProductionDogfoodStatus(
    bool Enabled,
    bool DevMode,
    bool ForkOrchestratorEnabled,
    string OrchestratorMode,
    IReadOnlyList<string> RequiredGames,
    ProductionDogfoodLastRun? LastRun);

public sealed record ProductionDogfoodLastRun(
    bool Passed,
    DateTimeOffset RunAtUtc,
    string? StreamerId,
    IReadOnlyList<string> VerifiedGames);
