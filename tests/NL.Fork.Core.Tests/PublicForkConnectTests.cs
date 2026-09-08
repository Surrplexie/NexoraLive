using NL.Fork.Core;
using Xunit;

namespace NL.Fork.Core.Tests;

public class PublicForkConnectTests
{
    [Fact]
    public void Rewrite_RimWorldLoopback_UsesPublicHost()
    {
        var published = PublicForkConnect.RewriteForPublic(
            "rimworld://127.0.0.1:25555",
            "play.example.com");
        Assert.Equal("rimworld://play.example.com:25555", published);
        Assert.False(PublicForkConnect.IsPrivateOrLoopbackEndpoint(published));
    }

    [Fact]
    public void Rewrite_KenshiLoopback_UsesPublicHost()
    {
        var published = PublicForkConnect.RewriteForPublic(
            "kenshi://127.0.0.1:23386",
            "play.example.com");
        Assert.Equal("kenshi://play.example.com:23386", published);
        Assert.False(PublicForkConnect.IsPrivateOrLoopbackEndpoint(published));
    }

    [Fact]
    public void Rewrite_WithoutPublicHost_KeepsLoopback()
    {
        var keys = new[]
        {
            PublicForkConnect.HostVariable,
            "NL_PUBLIC_HOST",
            "NL_VPS_DOMAIN",
            "NL_PUBLIC_BASE_URL",
            "NL_PUBLIC_HTTP",
        };
        var previous = keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var k in keys)
            {
                Environment.SetEnvironmentVariable(k, null);
            }

            var raw = "rimworld://127.0.0.1:25555";
            Assert.Equal(raw, PublicForkConnect.RewriteForPublic(raw));
        }
        finally
        {
            foreach (var kv in previous)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }
    }

    [Fact]
    public void Rewrite_DockerUri_Unchanged()
    {
        Assert.Equal(
            "docker://nl-fork-abc",
            PublicForkConnect.RewriteForPublic("docker://nl-fork-abc", "play.example.com"));
    }

    [Fact]
    public void T2LiteReady_RequiresNonLoopbackHost()
    {
        var previous = Environment.GetEnvironmentVariable(PublicForkConnect.HostVariable);
        try
        {
            Environment.SetEnvironmentVariable(PublicForkConnect.HostVariable, "play.example.com");
            Assert.True(PublicForkConnect.IsT2LiteReady());
            Environment.SetEnvironmentVariable(PublicForkConnect.HostVariable, "127.0.0.1");
            Assert.False(PublicForkConnect.IsT2LiteReady());
        }
        finally
        {
            Environment.SetEnvironmentVariable(PublicForkConnect.HostVariable, previous);
        }
    }
}
