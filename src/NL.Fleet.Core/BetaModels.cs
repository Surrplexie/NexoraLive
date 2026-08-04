namespace NL.Fleet.Core;

public enum BetaWaitlistStatus
{
    Pending,
    Approved,
    Rejected,
}

public sealed record BetaWaitlistEntry(
    string Id,
    string DisplayName,
    string Contact,
    string? TwitchHandle,
    string? RequestedGameId,
    BetaWaitlistStatus Status,
    string? ApprovedStreamerId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public sealed record BetaProgramStatus(
    bool Enabled,
    bool WaitlistOpen,
    int MaxApprovedStreamers,
    int PendingCount,
    int ApprovedCount,
    int RemainingSlots,
    DateTimeOffset GeneratedAtUtc);

public sealed record BetaStreamerCheckResult(
    bool Allowed,
    string? DenyReason = null);

public sealed record BetaValidationCheck(
    string Id,
    string Description,
    bool Passed,
    string? Detail = null);

public sealed record BetaValidationReport(
    bool BetaPassed,
    IReadOnlyList<BetaValidationCheck> Checks,
    DateTimeOffset EvaluatedAtUtc);
