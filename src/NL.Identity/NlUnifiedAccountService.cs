using NL.Identity.Core;

namespace NL.Identity;

/// <summary>
/// One NL account = identity record + SP player profile + optional streamer id.
/// Handles registration, login sessions, and capability provisioning.
/// </summary>
public sealed class NlUnifiedAccountService
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromDays(30);

    private readonly NlIdentityService _identity;
    private readonly IIdentityStore _store;
    private readonly IIdentityAuditStore _audit;
    private readonly AccountVerificationService _verification;
    private readonly JsonLoginSessionStore _sessions;
    private readonly IUnifiedAccountProvisioner? _provisioner;

    public NlUnifiedAccountService(
        NlIdentityService identity,
        IIdentityStore store,
        IIdentityAuditStore audit,
        AccountVerificationService verification,
        JsonLoginSessionStore sessions,
        IUnifiedAccountProvisioner? provisioner = null)
    {
        _identity = identity;
        _store = store;
        _audit = audit;
        _verification = verification;
        _sessions = sessions;
        _provisioner = provisioner;
    }

    public AuthRegisterResult Register(string displayName, string email, string password)
    {
        displayName = displayName.Trim();
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new AuthRegisterResult(false, Error: "displayName required.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return new AuthRegisterResult(false, Error: "Valid email required.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return new AuthRegisterResult(false, Error: "Password must be at least 8 characters.");
        }

        if (_store.GetAccountIdForEmail(email) is not null)
        {
            return new AuthRegisterResult(false, Error: "Email already registered.");
        }

        var account = _identity.CreateAccount(displayName);
        account.Email = email;
        account.ProtectedPasswordHash = NlPasswordHasher.HashPassword(password);
        _store.SaveAccount(account);

        _provisioner?.EnsurePlayerProfile(account.Id, displayName);

        var session = _sessions.Create(account.Id, SessionTtl);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.AccountRegistered,
            account.Id,
            null,
            $"Registered {email}",
            DateTimeOffset.UtcNow));

        return new AuthRegisterResult(true, session.Token, ToSummary(account));
    }

    public AuthLoginResult Login(string email, string password, string? twoFactorCode = null)
    {
        email = email.Trim().ToLowerInvariant();
        var accountId = _store.GetAccountIdForEmail(email);
        if (accountId is null)
        {
            return new AuthLoginResult(false, Error: "Invalid email or password.");
        }

        var account = _store.GetAccount(accountId);
        if (account is null || !NlPasswordHasher.VerifyPassword(password, account.ProtectedPasswordHash))
        {
            return new AuthLoginResult(false, Error: "Invalid email or password.");
        }

        if (account.TwoFactorEnabledAtUtc is not null)
        {
            if (string.IsNullOrWhiteSpace(twoFactorCode))
            {
                return new AuthLoginResult(false, RequiresTwoFactor: true, Error: "Two-factor code required.");
            }

            if (!_verification.VerifyTwoFactorCode(account, twoFactorCode))
            {
                return new AuthLoginResult(false, RequiresTwoFactor: true, Error: "Invalid two-factor code.");
            }
        }

        _provisioner?.EnsurePlayerProfile(account.Id, account.DisplayName);

        var session = _sessions.Create(account.Id, SessionTtl);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.AccountLoggedIn,
            account.Id,
            null,
            "Password login",
            DateTimeOffset.UtcNow));

        return new AuthLoginResult(true, session.Token, session.ExpiresAtUtc, ToSummary(account));
    }

    public bool Logout(string sessionToken)
    {
        var session = _sessions.GetValid(sessionToken);
        if (session is null)
        {
            return false;
        }

        _sessions.Revoke(sessionToken);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.AccountLoggedOut,
            session.AccountId,
            null,
            "Session revoked",
            DateTimeOffset.UtcNow));
        return true;
    }

    public NlIdentityAccount? GetAccountFromSession(string? sessionToken)
    {
        var session = _sessions.GetValid(sessionToken ?? "");
        return session is null ? null : _store.GetAccount(session.AccountId);
    }

    public UnifiedAccountSummary? GetSummaryFromSession(string? sessionToken)
    {
        var account = GetAccountFromSession(sessionToken);
        return account is null ? null : ToSummary(account);
    }

    public AuthLoginResult EnableStreamer(string accountId, string? streamerSlug = null)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");

        var streamerId = NormalizeStreamerId(streamerSlug, account);
        if (_store.GetAccountIdForStreamer(streamerId) is { } existing
            && !string.Equals(existing, accountId, StringComparison.OrdinalIgnoreCase))
        {
            return new AuthLoginResult(false, Error: $"Streamer id '{streamerId}' is already taken.");
        }

        account.StreamerId = streamerId;
        account.StreamerEnabledAtUtc = DateTimeOffset.UtcNow;
        _store.SaveAccount(account);
        _provisioner?.EnsureStreamerConfig(account.Id, streamerId, account.DisplayName);

        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.StreamerEnabled,
            account.Id,
            streamerId,
            "Streamer capability enabled",
            DateTimeOffset.UtcNow));

        return new AuthLoginResult(true, Account: ToSummary(account));
    }

    public UnifiedAccountSummary ToSummary(NlIdentityAccount account) => new(
        account.Id,
        account.DisplayName,
        account.PlayerId,
        account.Email,
        account.StreamerId,
        account.Capabilities,
        account.EmailVerifiedAtUtc is not null,
        account.TwoFactorEnabledAtUtc is not null);

    /// <summary>Ensure SP profile exists for legacy identity-only account creation.</summary>
    public void EnsurePlayerProfile(string accountId, string displayName) =>
        _provisioner?.EnsurePlayerProfile(accountId, displayName);

    private static string NormalizeStreamerId(string? slug, NlIdentityAccount account)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var trimmed = slug.Trim().ToLowerInvariant();
            var cleaned = new string(trimmed.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                return cleaned;
            }
        }

        return account.Id;
    }
}
