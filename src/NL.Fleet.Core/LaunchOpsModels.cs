namespace NL.Fleet.Core;

public sealed record LaunchStatusComponent(
    string Id,
    string Name,
    string Status,
    string? Detail = null);

public sealed record LaunchStatusPageSnapshot(
    string OverallStatus,
    IReadOnlyList<LaunchStatusComponent> Components,
    DateTimeOffset UpdatedAtUtc);

public sealed record LaunchBackupCheckResult(
    bool Passed,
    string? BackupRoot,
    DateTimeOffset? LastBackupUtc,
    string? Detail);

public sealed record LaunchOpsValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record LaunchOpsValidationReport(
    bool LaunchOpsPassed,
    IReadOnlyList<LaunchOpsValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);

public sealed record LaunchOpsStatus(
    bool Enabled,
    bool DevMode,
    bool StatusPageEnabled,
    bool HardeningEnabled,
    bool MultiGameEnabled,
    bool AlertWebhookConfigured,
    string? LegalVersion,
    string? BackupRoot,
    DateTimeOffset ObservedAtUtc);
