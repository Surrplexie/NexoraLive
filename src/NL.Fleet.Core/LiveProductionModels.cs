namespace NL.Fleet.Core;

public sealed record LiveProductionStatus(
    bool Enabled,
    bool DevMode,
    bool GaEnabled,
    bool SteamConfigured,
    string? IdentityMode,
    string? PublicBaseUrl,
    string? RelayTemplate,
    string? TurnUri,
    DateTimeOffset GeneratedAtUtc);

public sealed record LiveProductionValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record LiveProductionValidationReport(
    bool LiveProductionPassed,
    IReadOnlyList<LiveProductionValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);
