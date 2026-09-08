using System.Security.Cryptography;
using NL.Core.Sp;
using NL.Identity.Core;

namespace NL.Identity;

public sealed record EmailVerificationRequestResult(
    bool Success,
    string? DevCode = null,
    string? Error = null);

public sealed record TwoFactorEnrollStartResult(
    bool Success,
    string? SecretBase32 = null,
    string? OtpAuthUri = null,
    string? Error = null);

public sealed record VerificationActionResult(
    bool Success,
    SpVerification Verification = SpVerification.None,
    string? Error = null);

public sealed class AccountVerificationService
{
    private readonly NlIdentityService _identity;
    private readonly IIdentityStore _store;
    private readonly IIdentityAuditStore _audit;
    private readonly JsonEmailVerificationChallengeStore _emailChallenges;
    private readonly IEmailSender _emailSender;
    private readonly NlTokenProtector _tokenProtector;

    public AccountVerificationService(
        NlIdentityService identity,
        IIdentityStore store,
        IIdentityAuditStore audit,
        JsonEmailVerificationChallengeStore emailChallenges,
        IEmailSender emailSender,
        NlTokenProtector? tokenProtector = null)
    {
        _identity = identity;
        _store = store;
        _audit = audit;
        _emailChallenges = emailChallenges;
        _emailSender = emailSender;
        _tokenProtector = tokenProtector ?? new NlTokenProtector();
    }

    public SpVerification ComputeVerificationFlags(NlIdentityAccount account)
    {
        var flags = SpVerification.None;
        if (account.EmailVerifiedAtUtc is not null)
        {
            flags |= SpVerification.Email;
        }

        if (account.TwoFactorEnabledAtUtc is not null)
        {
            flags |= SpVerification.TwoFactor;
        }

        return flags;
    }

    public VerificationStatus GetStatus(NlIdentityAccount account) => new(
        account.Id,
        account.Email,
        account.EmailVerifiedAtUtc,
        account.TwoFactorEnabledAtUtc is not null,
        ComputeVerificationFlags(account));

    public async Task<EmailVerificationRequestResult> RequestEmailVerificationAsync(
        string accountId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");

        email = email.Trim();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return new EmailVerificationRequestResult(false, Error: "Valid email required.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _emailChallenges.Save(new EmailVerificationChallenge(
            account.Id,
            email,
            code,
            DateTimeOffset.UtcNow.AddMinutes(10)));

        account.Email = email;
        account.EmailVerifiedAtUtc = null;
        _store.SaveAccount(account);

        await _emailSender.SendVerificationCodeAsync(email, code, cancellationToken);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.EmailVerificationSent,
            account.Id,
            null,
            $"Verification code sent to {email}",
            DateTimeOffset.UtcNow));

        var exposeDevCode = string.Equals(
            Environment.GetEnvironmentVariable("NL_VERIFICATION_DEV_EXPOSE_CODES"),
            "1",
            StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable("NL_VERIFICATION_DEV_EXPOSE_CODES"),
                "true",
                StringComparison.OrdinalIgnoreCase);

        return new EmailVerificationRequestResult(true, exposeDevCode ? code : null);
    }

    public VerificationActionResult ConfirmEmailVerification(string accountId, string code)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");

        var challenge = _emailChallenges.Consume(accountId, code);
        if (challenge is null)
        {
            return new VerificationActionResult(false, ComputeVerificationFlags(account), "Invalid or expired verification code.");
        }

        account.Email = challenge.Email;
        account.EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
        _store.SaveAccount(account);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.EmailVerified,
            account.Id,
            null,
            $"Email verified: {challenge.Email}",
            DateTimeOffset.UtcNow));

        return new VerificationActionResult(true, ComputeVerificationFlags(account));
    }

    public TwoFactorEnrollStartResult StartTwoFactorEnrollment(string accountId)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");

        if (account.TwoFactorEnabledAtUtc is not null)
        {
            return new TwoFactorEnrollStartResult(false, Error: "Two-factor authentication is already enabled.");
        }

        var secret = TotpHelper.GenerateSecretBase32();
        account.ProtectedTotpSecret = _tokenProtector.Protect(secret);
        account.TwoFactorEnabledAtUtc = null;
        _store.SaveAccount(account);

        var label = string.IsNullOrWhiteSpace(account.Email) ? account.DisplayName : account.Email!;
        var uri = TotpHelper.BuildOtpAuthUri(label, secret);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.TwoFactorEnrollStarted,
            account.Id,
            null,
            "2FA enrollment started",
            DateTimeOffset.UtcNow));

        return new TwoFactorEnrollStartResult(true, secret, uri);
    }

    public VerificationActionResult ConfirmTwoFactorEnrollment(string accountId, string code)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");

        var secret = UnprotectSecret(account.ProtectedTotpSecret);
        if (secret is null)
        {
            return new VerificationActionResult(false, ComputeVerificationFlags(account), "Start 2FA enrollment first.");
        }

        if (!TotpHelper.VerifyCode(secret, code, DateTimeOffset.UtcNow))
        {
            return new VerificationActionResult(false, ComputeVerificationFlags(account), "Invalid authenticator code.");
        }

        account.TwoFactorEnabledAtUtc = DateTimeOffset.UtcNow;
        _store.SaveAccount(account);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.TwoFactorEnabled,
            account.Id,
            null,
            "2FA enabled",
            DateTimeOffset.UtcNow));

        return new VerificationActionResult(true, ComputeVerificationFlags(account));
    }

    public bool VerifyTwoFactorCode(NlIdentityAccount account, string code)
    {
        if (account.TwoFactorEnabledAtUtc is null)
        {
            return false;
        }

        var secret = UnprotectSecret(account.ProtectedTotpSecret);
        if (secret is null)
        {
            return false;
        }

        var ok = TotpHelper.VerifyCode(secret, code, DateTimeOffset.UtcNow);
        if (!ok)
        {
            _audit.Append(new NlIdentityAuditEvent(
                NlIdentityAuditKind.TwoFactorChallengeFailed,
                account.Id,
                null,
                "2FA challenge failed",
                DateTimeOffset.UtcNow));
        }

        return ok;
    }

    public VerificationActionResult DisableTwoFactor(string accountId, string code)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");

        if (account.TwoFactorEnabledAtUtc is null)
        {
            return new VerificationActionResult(true, ComputeVerificationFlags(account));
        }

        if (!VerifyTwoFactorCode(account, code))
        {
            return new VerificationActionResult(false, ComputeVerificationFlags(account), "Invalid authenticator code.");
        }

        account.ProtectedTotpSecret = null;
        account.TwoFactorEnabledAtUtc = null;
        _store.SaveAccount(account);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.TwoFactorDisabled,
            account.Id,
            null,
            "2FA disabled",
            DateTimeOffset.UtcNow));

        return new VerificationActionResult(true, ComputeVerificationFlags(account));
    }

    public void ApplyVerificationToProfile(NlIdentityAccount account, SpProfile profile)
    {
        profile.NlAccountId = account.Id;
        profile.Verification = ComputeVerificationFlags(account);
    }

    private string? UnprotectSecret(string? protectedSecret) =>
        string.IsNullOrWhiteSpace(protectedSecret) ? null : _tokenProtector.Unprotect(protectedSecret);
}

public sealed record VerificationStatus(
    string AccountId,
    string? Email,
    DateTimeOffset? EmailVerifiedAtUtc,
    bool TwoFactorEnabled,
    SpVerification VerificationFlags);
