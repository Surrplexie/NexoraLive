using NL.Core.Sp;
using NL.Identity;
using NL.Identity.Core;
using Xunit;

namespace NL.Identity.Tests;

public class TotpHelperTests
{
    [Fact]
    public void VerifyCode_AcceptsCurrentWindow()
    {
        var secret = TotpHelper.GenerateSecretBase32();
        var now = DateTimeOffset.UtcNow;
        var code = TotpHelper.CurrentCode(secret, now);

        Assert.True(TotpHelper.VerifyCode(secret, code, now));
    }

    [Fact]
    public void VerifyCode_RejectsWrongCode()
    {
        var secret = TotpHelper.GenerateSecretBase32();
        Assert.False(TotpHelper.VerifyCode(secret, "000000", DateTimeOffset.UtcNow));
    }
}

public class AccountVerificationServiceTests
{
    [Fact]
    public async Task EmailVerification_SetsEmailFlag()
    {
        Environment.SetEnvironmentVariable("NL_VERIFICATION_DEV_EXPOSE_CODES", "1");
        var root = Path.Combine(Path.GetTempPath(), "nl-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var store = new JsonFileIdentityStore(root);
        var audit = new JsonlIdentityAuditStore(Path.Combine(root, "audit.jsonl"));
        var identity = new NlIdentityService(store, audit);
        var account = identity.CreateAccount("verify-test");
        var challenges = new JsonEmailVerificationChallengeStore(Path.Combine(root, "email-challenges.json"));
        var svc = new AccountVerificationService(identity, store, audit, challenges, new MockEmailSender());

        var requested = await svc.RequestEmailVerificationAsync(account.Id, "player@example.com");
        Assert.True(requested.Success);
        Assert.NotNull(requested.DevCode);

        var confirmed = svc.ConfirmEmailVerification(account.Id, requested.DevCode!);
        Assert.True(confirmed.Success);
        Assert.True((confirmed.Verification & SpVerification.Email) != 0);

        var reloaded = store.GetAccount(account.Id)!;
        Assert.Equal("player@example.com", reloaded.Email);
        Assert.NotNull(reloaded.EmailVerifiedAtUtc);
    }

    [Fact]
    public void TwoFactorEnrollment_SetsTwoFactorFlag()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-2fa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var store = new JsonFileIdentityStore(root);
        var audit = new JsonlIdentityAuditStore(Path.Combine(root, "audit.jsonl"));
        var identity = new NlIdentityService(store, audit);
        var account = identity.CreateAccount("2fa-test");
        var challenges = new JsonEmailVerificationChallengeStore(Path.Combine(root, "email-challenges.json"));
        var svc = new AccountVerificationService(identity, store, audit, challenges, new MockEmailSender());

        var started = svc.StartTwoFactorEnrollment(account.Id);
        Assert.True(started.Success);
        Assert.NotNull(started.SecretBase32);

        var code = TotpHelper.CurrentCode(started.SecretBase32!, DateTimeOffset.UtcNow);
        var confirmed = svc.ConfirmTwoFactorEnrollment(account.Id, code);
        Assert.True(confirmed.Success);
        Assert.True((confirmed.Verification & SpVerification.TwoFactor) != 0);
    }

    [Fact]
    public void ApplyVerificationToProfile_CopiesFlagsAndAccountId()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-apply-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var store = new JsonFileIdentityStore(root);
        var audit = new JsonlIdentityAuditStore(Path.Combine(root, "audit.jsonl"));
        var identity = new NlIdentityService(store, audit);
        var account = identity.CreateAccount("apply-test");
        account.EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
        account.TwoFactorEnabledAtUtc = DateTimeOffset.UtcNow;
        store.SaveAccount(account);

        var challenges = new JsonEmailVerificationChallengeStore(Path.Combine(root, "email-challenges.json"));
        var svc = new AccountVerificationService(identity, store, audit, challenges, new MockEmailSender());
        var profile = new SpProfile
        {
            Id = "player-1",
            DisplayName = "player-1",
            AccountCreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
        };

        svc.ApplyVerificationToProfile(account, profile);

        Assert.Equal(account.Id, profile.NlAccountId);
        Assert.True((profile.Verification & SpVerification.Email) != 0);
        Assert.True((profile.Verification & SpVerification.TwoFactor) != 0);
    }
}
