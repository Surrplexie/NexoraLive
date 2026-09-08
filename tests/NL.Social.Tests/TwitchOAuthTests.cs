using NL.Social;
using NL.Social.Core;
using Xunit;

namespace NL.Social.Tests;

public class TwitchOAuthStateTests
{
    [Fact]
    public void StateStore_CreateAndConsume_Works()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-twitch-oauth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new JsonSocialOAuthStateStore(Path.Combine(dir, "oauth-state.json"));

        var state = store.Create("player-a", "/social-link.html", TimeSpan.FromMinutes(5));
        var pending = store.Consume(state);

        Assert.NotNull(pending);
        Assert.Equal("player-a", pending!.PlayerId);
        Assert.Equal("/social-link.html", pending.ReturnUrl);
        Assert.Null(store.Consume(state));
    }
}

public class TwitchOAuthCredentialStoreTests
{
    [Fact]
    public void CredentialStore_RejectsDuplicateTwitchUser()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-twitch-cred-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new JsonTwitchOAuthCredentialStore(Path.Combine(dir, "creds.json"));

        store.Save(new TwitchOAuthCredential("player-a", "12345", "alice", "protected-a"));
        var ex = Assert.Throws<TwitchLinkConflictException>(() =>
            store.Save(new TwitchOAuthCredential("player-b", "12345", "alice", "protected-b")));

        Assert.Equal("12345", ex.TwitchUserId);
        Assert.Equal("player-a", ex.ExistingPlayerId);
    }

    [Fact]
    public void BuildAuthorizeRedirect_IncludesRequiredParams()
    {
        Environment.SetEnvironmentVariable("TWITCH_CLIENT_ID", "test-client-id");
        Environment.SetEnvironmentVariable("TWITCH_CLIENT_SECRET", "test-secret");

        var dir = Path.Combine(Path.GetTempPath(), "nl-twitch-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var stateStore = new JsonSocialOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
        var credStore = new JsonTwitchOAuthCredentialStore(Path.Combine(dir, "creds.json"));
        var linkStore = new JsonSpSocialLinkStore(Path.Combine(dir, "links.json"));
        var svc = new TwitchOAuthService(stateStore, credStore, linkStore);

        var url = svc.BuildAuthorizeRedirect("player-1", "/social-link.html", "http://127.0.0.1:27020");

        Assert.Contains("client_id=test-client-id", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains(Uri.EscapeDataString(TwitchOAuthService.DefaultScopes), url);
        Assert.Contains(Uri.EscapeDataString("http://127.0.0.1:27020/api/v1/social/oauth/twitch/callback"), url);
        Assert.True(svc.IsConfigured);
    }
}
