namespace NL.Fleet.Core;

public sealed record PublicGaLaunchChecklistItem(
    string Id,
    string Category,
    string Title,
    string Description,
    bool Required,
    string? DocPath = null);

public sealed record PublicGaLaunchChecklist(
    string LaunchVersion,
    IReadOnlyList<PublicGaLaunchChecklistItem> Items,
    DateTimeOffset GeneratedAtUtc);

public sealed record PublicGaLaunchValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record PublicGaLaunchValidationReport(
    bool PublicGaLaunchPassed,
    IReadOnlyList<PublicGaLaunchValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);

public sealed record PublicGaLaunchStatus(
    bool Enabled,
    bool DevMode,
    bool GaOpenSignup,
    string? SupportContact,
    string LaunchVersion,
    bool LegalComplianceEnabled,
    bool AllProgramsEnabled,
    DateTimeOffset ObservedAtUtc);

public sealed record PublicGaLaunchSignoffEntry(
    string OperatorId,
    string LaunchVersion,
    DateTimeOffset SignedAtUtc);
