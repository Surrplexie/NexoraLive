using NL.Identity;
using NL.Identity.Core;
using Xunit;

namespace NL.Identity.Tests;

public class SteamOpenIdServiceTests
{
    [Theory]
    [InlineData("https://steamcommunity.com/openid/id/76561198000000001", "76561198000000001")]
    [InlineData("https://steamcommunity.com/openid/id/76561198999999999/", "76561198999999999")]
    public void ExtractSteamId_ParsesClaimedId(string claimed, string expected)
    {
        Assert.Equal(expected, SteamOpenIdService.ExtractSteamId(claimed));
    }

    [Fact]
    public void ExtractSteamId_Invalid_ReturnsNull()
    {
        Assert.Null(SteamOpenIdService.ExtractSteamId("https://example.com/not-steam"));
        Assert.Null(SteamOpenIdService.ExtractSteamId(null));
    }

    [Fact]
    public void BuildAuthorizeRedirect_IncludesCallbackAndState()
    {
        var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var store = new JsonOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
            var svc = new SteamOpenIdService(store, new NlIdentitySettings { SteamRealm = "http://test.local" });
            var url = svc.BuildAuthorizeRedirect("acct-99", "/nl-client.html", "http://test.local");
            Assert.StartsWith("https://steamcommunity.com/openid/login?", url);
            Assert.Contains("openid.return_to=", url);
            Assert.Contains("openid.realm=", url);
            Assert.Contains(Uri.EscapeDataString("http://test.local/api/v1/identity/oauth/steam/callback"), url);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class OAuthStateStoreTests
{
    [Fact]
    public void CreateAndConsume_RoundTrip()
    {
        var dir = IdentityTestHelpers.NewTempDir();
        try
        {
            var store = new JsonOAuthStateStore(Path.Combine(dir, "oauth-state.json"));
            var state = store.Create("acct-1", "/identity-link.html", TimeSpan.FromMinutes(5));
            var entry = store.Consume(state);
            Assert.NotNull(entry);
            Assert.Equal("acct-1", entry!.AccountId);
            Assert.Equal("/identity-link.html", entry.ReturnUrl);
            Assert.Null(store.Consume(state));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class IdentityEnabledGateTests
{
    [Fact]
    public async Task OwnershipGate_SkipsWhenIdentityDisabled()
    {
        var identity = new NlIdentityHost(new NlIdentitySettings
        {
            Enabled = false,
            Mode = NlOwnershipMode.Mock,
        });

        var gate = identity.OwnershipGate;
        var result = await gate.EvaluateAsync(new OwnershipAdmissionContext(
            RequireGameOwnership: true,
            Mode: NlOwnershipMode.Mock,
            Platform: "steam",
            PlatformUserId: "76561198000000001",
            GameId: "hello-fork",
            AppId: "730",
            MajorVersion: null,
            NlAccountId: null,
            StrictUnknown: true));

        Assert.Null(result);
    }
}
