using NL.Identity;
using NL.Identity.Core;
using Xunit;

namespace NL.Identity.Tests;

public class PlatformOAuthTests
{
    [Fact]
    public void EpicAuthorize_IncludesRequiredParams()
    {
        Environment.SetEnvironmentVariable("EPIC_CLIENT_ID", "epic-client");
        Environment.SetEnvironmentVariable("EPIC_CLIENT_SECRET", "epic-secret");

        var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var stateStore = new JsonOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
            var credStore = new JsonPlatformOAuthCredentialStore(Path.Combine(dir, "creds.json"));
            var identity = new NlIdentityService(new JsonFileIdentityStore(dir), new JsonlIdentityAuditStore(Path.Combine(dir, "audit.jsonl")));
            var svc = new EpicOAuthService(stateStore, credStore, identity);

            var url = svc.BuildAuthorizeRedirect("acct-1", "/identity-link.html", "http://127.0.0.1:27020");
            Assert.Contains("client_id=epic-client", url);
            Assert.Contains(Uri.EscapeDataString("http://127.0.0.1:27020/api/v1/identity/oauth/epic/callback"), url);
            Assert.True(svc.IsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EPIC_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("EPIC_CLIENT_SECRET", null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void XboxAuthorize_IncludesRequiredParams()
    {
        Environment.SetEnvironmentVariable("XBOX_CLIENT_ID", "xbox-client");
        Environment.SetEnvironmentVariable("XBOX_CLIENT_SECRET", "xbox-secret");

        var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var stateStore = new JsonOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
            var credStore = new JsonPlatformOAuthCredentialStore(Path.Combine(dir, "creds.json"));
            var identity = new NlIdentityService(new JsonFileIdentityStore(dir), new JsonlIdentityAuditStore(Path.Combine(dir, "audit.jsonl")));
            var svc = new XboxOAuthService(stateStore, credStore, identity);

            var url = svc.BuildAuthorizeRedirect("acct-1", "/identity-link.html", "http://127.0.0.1:27020");
            Assert.Contains("client_id=xbox-client", url);
            Assert.Contains(Uri.EscapeDataString(XboxOAuthService.DefaultScopes), url);
            Assert.True(svc.IsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XBOX_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("XBOX_CLIENT_SECRET", null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PlayStationAuthorize_IncludesRequiredParams()
    {
        Environment.SetEnvironmentVariable("PSN_CLIENT_ID", "psn-client");
        Environment.SetEnvironmentVariable("PSN_CLIENT_SECRET", "psn-secret");

        var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var stateStore = new JsonOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
            var credStore = new JsonPlatformOAuthCredentialStore(Path.Combine(dir, "creds.json"));
            var identity = new NlIdentityService(new JsonFileIdentityStore(dir), new JsonlIdentityAuditStore(Path.Combine(dir, "audit.jsonl")));
            var svc = new PlayStationOAuthService(stateStore, credStore, identity);

            var url = svc.BuildAuthorizeRedirect("acct-1", "/identity-link.html", "http://127.0.0.1:27020");
            Assert.Contains("client_id=psn-client", url);
            Assert.Contains(Uri.EscapeDataString(PlayStationOAuthService.DefaultScopes), url);
            Assert.True(svc.IsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PSN_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("PSN_CLIENT_SECRET", null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CredentialStore_RejectsDuplicatePlatformUser()
    {
        var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var store = new JsonPlatformOAuthCredentialStore(Path.Combine(dir, "creds.json"));
            store.Save(new PlatformOAuthCredential("acct-a", NlPlatform.Epic, "epic-123", "Alice", "protected"));
            var ex = Assert.Throws<PlatformLinkConflictException>(() =>
                store.Save(new PlatformOAuthCredential("acct-b", NlPlatform.Epic, "epic-123", "Bob", "protected2")));
            Assert.Equal("acct-a", ex.ExistingAccountId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LiveHost_WiresEpicXboxPlayStationOAuth()
    {
        var host = new NlIdentityHost(new NlIdentitySettings { Enabled = true, Mode = NlOwnershipMode.Mock });
        Assert.NotNull(host.EpicOAuth);
        Assert.NotNull(host.XboxOAuth);
        Assert.NotNull(host.PlayStationOAuth);
        Assert.NotNull(host.PlatformCredentials);
    }
}
