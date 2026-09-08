using NL.Social;
using NL.Social.Core;
using Xunit;

namespace NL.Social.Tests;

public class DiscordOAuthStateTests
{
    [Fact]
    public void CredentialStore_RejectsDuplicateDiscordUser()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-discord-cred-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new JsonDiscordOAuthCredentialStore(Path.Combine(dir, "creds.json"));

        store.Save(new DiscordOAuthCredential("player-a", "999", "alice", "protected-a"));
        var ex = Assert.Throws<DiscordLinkConflictException>(() =>
            store.Save(new DiscordOAuthCredential("player-b", "999", "alice", "protected-b")));

        Assert.Equal("999", ex.DiscordUserId);
        Assert.Equal("player-a", ex.ExistingPlayerId);
    }

    [Fact]
    public void BuildAuthorizeRedirect_IncludesRequiredParams()
    {
        Environment.SetEnvironmentVariable("DISCORD_CLIENT_ID", "test-discord-client");
        Environment.SetEnvironmentVariable("DISCORD_CLIENT_SECRET", "test-secret");

        var dir = Path.Combine(Path.GetTempPath(), "nl-discord-auth-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var stateStore = new JsonSocialOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
        var credStore = new JsonDiscordOAuthCredentialStore(Path.Combine(dir, "creds.json"));
        var linkStore = new JsonSpSocialLinkStore(Path.Combine(dir, "links.json"));
        var svc = new DiscordOAuthService(stateStore, credStore, linkStore);

        var url = svc.BuildAuthorizeRedirect("player-1", "/social-link.html", "http://127.0.0.1:27020");

        Assert.Contains("client_id=test-discord-client", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains(Uri.EscapeDataString(DiscordOAuthService.DefaultScopes), url);
        Assert.Contains(Uri.EscapeDataString("http://127.0.0.1:27020/api/v1/social/oauth/discord/callback"), url);
        Assert.True(svc.IsConfigured);
    }
}

public class LiveSocialRelationshipProviderTests
{
    [Fact]
    public async Task LiveProvider_OverridesDiscordMembership()
    {
        var twitch = new StubSocialProvider(new SocialRelationshipStatus(true, false, false, "twitch-oauth"));
        var discord = new StubDiscordChecker(true);
        var live = new LiveSocialRelationshipProvider(twitch, discord);

        var status = await live.GetStatusAsync(new SocialGateContext(
            "streamer",
            "player",
            new SpSocialLinks("player", DiscordUserId: "123"),
            new StreamerSocialConfig("streamer", DiscordGuildId: "guild-1"),
            true,
            false,
            true));

        Assert.True(status.IsFollowing);
        Assert.False(status.IsSubscribed);
        Assert.True(status.IsDiscordMember);
        Assert.Contains("discord-oauth", status.Source);
    }

    private sealed class StubSocialProvider(SocialRelationshipStatus status) : ISocialRelationshipProvider
    {
        public Task<SocialRelationshipStatus> GetStatusAsync(
            SocialGateContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(status);
    }

    private sealed class StubDiscordChecker(bool? member) : IDiscordGuildMembershipChecker
    {
        public Task<bool?> TryGetMembershipAsync(
            SocialGateContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(member);
    }
}
