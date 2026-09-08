using NL.Social;
using NL.Social.Core;
using Xunit;

namespace NL.Social.Tests;

public class YouTubeOAuthCredentialStoreTests
{
    [Fact]
    public void CredentialStore_RejectsDuplicateYouTubeChannel()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-youtube-cred-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new JsonYouTubeOAuthCredentialStore(Path.Combine(dir, "creds.json"));

        store.Save(new YouTubeOAuthCredential("player-a", "UC123", "Channel A", "protected-a"));
        var ex = Assert.Throws<YouTubeLinkConflictException>(() =>
            store.Save(new YouTubeOAuthCredential("player-b", "UC123", "Channel A", "protected-b")));

        Assert.Equal("UC123", ex.YouTubeChannelId);
        Assert.Equal("player-a", ex.ExistingPlayerId);
    }

    [Fact]
    public void BuildAuthorizeRedirect_IncludesRequiredParams()
    {
        Environment.SetEnvironmentVariable("YOUTUBE_CLIENT_ID", "test-youtube-client");
        Environment.SetEnvironmentVariable("YOUTUBE_CLIENT_SECRET", "test-secret");

        var dir = Path.Combine(Path.GetTempPath(), "nl-youtube-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var stateStore = new JsonSocialOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
        var credStore = new JsonYouTubeOAuthCredentialStore(Path.Combine(dir, "creds.json"));
        var linkStore = new JsonSpSocialLinkStore(Path.Combine(dir, "links.json"));
        var svc = new YouTubeOAuthService(stateStore, credStore, linkStore);

        var url = svc.BuildAuthorizeRedirect("player-1", "/social-link.html", "http://127.0.0.1:27020");

        Assert.Contains("client_id=test-youtube-client", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains(Uri.EscapeDataString(YouTubeOAuthService.DefaultScopes), url);
        Assert.Contains(Uri.EscapeDataString("http://127.0.0.1:27020/api/v1/social/oauth/youtube/callback"), url);
        Assert.True(svc.IsConfigured);
    }
}

public class KickOAuthCredentialStoreTests
{
    [Fact]
    public void CredentialStore_RejectsDuplicateKickUser()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-kick-cred-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new JsonKickOAuthCredentialStore(Path.Combine(dir, "creds.json"));

        store.Save(new KickOAuthCredential("player-a", "4242", "alice", "protected-a"));
        var ex = Assert.Throws<KickLinkConflictException>(() =>
            store.Save(new KickOAuthCredential("player-b", "4242", "alice", "protected-b")));

        Assert.Equal("4242", ex.KickUserId);
        Assert.Equal("player-a", ex.ExistingPlayerId);
    }

    [Fact]
    public void BuildAuthorizeRedirect_IncludesPkceParams()
    {
        Environment.SetEnvironmentVariable("KICK_CLIENT_ID", "test-kick-client");
        Environment.SetEnvironmentVariable("KICK_CLIENT_SECRET", "test-secret");

        var dir = Path.Combine(Path.GetTempPath(), "nl-kick-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var stateStore = new JsonSocialOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
        var credStore = new JsonKickOAuthCredentialStore(Path.Combine(dir, "creds.json"));
        var linkStore = new JsonSpSocialLinkStore(Path.Combine(dir, "links.json"));
        var svc = new KickOAuthService(stateStore, credStore, linkStore);

        var url = svc.BuildAuthorizeRedirect("player-1", "/social-link.html", "http://127.0.0.1:27020");

        Assert.Contains("client_id=test-kick-client", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("code_challenge=", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains(Uri.EscapeDataString(KickOAuthService.DefaultScopes), url);
        Assert.Contains(Uri.EscapeDataString("http://127.0.0.1:27020/api/v1/social/oauth/kick/callback"), url);
        Assert.True(svc.IsConfigured);
    }
}

public class KickPkceHelperTests
{
    [Fact]
    public void PkceChallenge_MatchesVerifier()
    {
        var verifier = KickPkceHelper.GenerateVerifier();
        var challenge = KickPkceHelper.ComputeChallenge(verifier);

        Assert.False(string.IsNullOrWhiteSpace(verifier));
        Assert.False(string.IsNullOrWhiteSpace(challenge));
        Assert.DoesNotContain("+", challenge);
        Assert.DoesNotContain("/", challenge);
        Assert.DoesNotContain("=", challenge);
    }
}
