namespace NL.Identity.Core;

[Flags]
public enum NlAccountCapabilities
{
    None = 0,
    Player = 1 << 0,
    Streamer = 1 << 1,
}

public sealed record NlLoginSession(
    string Token,
    string AccountId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record UnifiedAccountSummary(
    string AccountId,
    string DisplayName,
    string PlayerId,
    string? Email,
    string? StreamerId,
    NlAccountCapabilities Capabilities,
    bool EmailVerified,
    bool TwoFactorEnabled);

public sealed record AuthLoginResult(
    bool Success,
    string? SessionToken = null,
    DateTimeOffset? ExpiresAtUtc = null,
    UnifiedAccountSummary? Account = null,
    bool RequiresTwoFactor = false,
    string? Error = null);

public sealed record AuthRegisterResult(
    bool Success,
    string? SessionToken = null,
    UnifiedAccountSummary? Account = null,
    string? Error = null);

public interface IUnifiedAccountProvisioner
{
    void EnsurePlayerProfile(string accountId, string displayName);

    void EnsureStreamerConfig(string accountId, string streamerId, string displayName);
}
