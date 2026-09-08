using NL.Identity;
using NL.Identity.Core;
using NL.Core.Sp;
using Xunit;

namespace NL.Identity.Tests;

public class NlPasswordHasherTests
{
    [Fact]
    public void HashAndVerify_Works()
    {
        var hash = NlPasswordHasher.HashPassword("correct-horse-battery");
        Assert.True(NlPasswordHasher.VerifyPassword("correct-horse-battery", hash));
        Assert.False(NlPasswordHasher.VerifyPassword("wrong", hash));
    }
}

public class NlUnifiedAccountServiceTests
{
    [Fact]
    public void Register_Login_ProvisionsPlayerAndSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-unified-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var store = new JsonFileIdentityStore(root);
        var audit = new JsonlIdentityAuditStore(Path.Combine(root, "audit.jsonl"));
        var identity = new NlIdentityService(store, audit);
        var challenges = new JsonEmailVerificationChallengeStore(Path.Combine(root, "email-challenges.json"));
        var verification = new AccountVerificationService(identity, store, audit, challenges, new MockEmailSender());
        var sessions = new JsonLoginSessionStore(Path.Combine(root, "sessions.json"));

        var provisioned = new List<string>();
        var svc = new NlUnifiedAccountService(
            identity,
            store,
            audit,
            verification,
            sessions,
            new StubProvisioner(provisioned));

        var reg = svc.Register("Alice", "alice@example.com", "password123");
        Assert.True(reg.Success);
        Assert.NotNull(reg.SessionToken);
        Assert.Equal(NlAccountCapabilities.Player, reg.Account!.Capabilities);

        var login = svc.Login("alice@example.com", "password123");
        Assert.True(login.Success);
        Assert.NotNull(login.SessionToken);

        var account = svc.GetAccountFromSession(login.SessionToken);
        Assert.NotNull(account);
        Assert.Equal("alice@example.com", account!.Email);
        Assert.Contains(account.Id, provisioned);
    }

    [Fact]
    public void EnableStreamer_SetsCapabilityAndProvisioner()
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-unified-streamer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var store = new JsonFileIdentityStore(root);
        var audit = new JsonlIdentityAuditStore(Path.Combine(root, "audit.jsonl"));
        var identity = new NlIdentityService(store, audit);
        var challenges = new JsonEmailVerificationChallengeStore(Path.Combine(root, "email-challenges.json"));
        var verification = new AccountVerificationService(identity, store, audit, challenges, new MockEmailSender());
        var sessions = new JsonLoginSessionStore(Path.Combine(root, "sessions.json"));

        var streamers = new List<string>();
        var svc = new NlUnifiedAccountService(
            identity,
            store,
            audit,
            verification,
            sessions,
            new StubProvisioner([], streamers));

        var reg = svc.Register("Bob Streamer", "bob@example.com", "password12345");
        var result = svc.EnableStreamer(reg.Account!.AccountId, "bob-live");
        Assert.True(result.Success);
        Assert.True(result.Account!.Capabilities.HasFlag(NlAccountCapabilities.Streamer));
        Assert.Equal("bob-live", result.Account.StreamerId);
        Assert.Contains("bob-live", streamers);
    }

    private sealed class StubProvisioner(List<string> players, List<string>? streamers = null) : IUnifiedAccountProvisioner
    {
        public void EnsurePlayerProfile(string accountId, string displayName) => players.Add(accountId);

        public void EnsureStreamerConfig(string accountId, string streamerId, string displayName) =>
            streamers?.Add(streamerId);
    }
}
