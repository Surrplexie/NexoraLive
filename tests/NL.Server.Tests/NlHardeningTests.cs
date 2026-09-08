using NL.Server;
using NL.Server.Core.Security;
using Xunit;

namespace NL.Server.Tests;

public class NlHardeningSettingsTests
{
    [Fact]
    public void LoadFromEnvironment_DefaultsOnWhenPublicMode()
    {
        using var env = EnvScope.With((NlHardeningSettings.EnabledVariable, null));
        var settings = NlHardeningSettings.LoadFromEnvironment(publicMode: true);
        Assert.True(settings.Enabled);
        Assert.Equal(120, settings.AdmitRatePerMinute);
    }

    [Fact]
    public void LoadFromEnvironment_CanDisableExplicitly()
    {
        using var env = EnvScope.With(
            (NlHardeningSettings.EnabledVariable, "false"),
            (NlHardeningSettings.AdmitRateVariable, "10"));
        var settings = NlHardeningSettings.LoadFromEnvironment(publicMode: true);
        Assert.False(settings.Enabled);
    }

    [Fact]
    public void DemoSessionMaxHours_ZeroDisablesCap()
    {
        using var env = EnvScope.With((NlHardeningSettings.DemoSessionMaxHoursVariable, "0"));
        var settings = NlHardeningSettings.LoadFromEnvironment();
        Assert.Equal(TimeSpan.Zero, settings.DemoSessionMaxDuration);
    }
}

public class NlPublicRateLimitServiceTests
{
    [Fact]
    public void AdmitLimit_BlocksAfterThreshold()
    {
        var settings = new NlHardeningSettings { Enabled = true, AdmitRatePerMinute = 2 };
        var service = new NlPublicRateLimitService(settings);
        Assert.True(service.TryAdmit("1.2.3.4"));
        Assert.True(service.TryAdmit("1.2.3.4"));
        Assert.False(service.TryAdmit("1.2.3.4"));
    }

    [Fact]
    public void WhenDisabled_AlwaysAllows()
    {
        var service = new NlPublicRateLimitService(new NlHardeningSettings { Enabled = false });
        for (var i = 0; i < 10; i++)
        {
            Assert.True(service.TryAdmit("x"));
        }
    }
}

public class NlWebSocketConnectionGuardTests
{
    [Fact]
    public void TryAccept_EnforcesGlobalAndPerIpCaps()
    {
        var settings = new NlHardeningSettings
        {
            Enabled = true,
            WebSocketMaxConnections = 2,
            WebSocketMaxConnectionsPerIp = 1,
            WebSocketConnectRatePerMinute = 100,
        };
        var guard = new NlWebSocketConnectionGuard(settings);

        Assert.True(guard.TryAccept("10.0.0.1", out _));
        Assert.False(guard.TryAccept("10.0.0.1", out var reason));
        Assert.Contains("per IP", reason, StringComparison.OrdinalIgnoreCase);

        Assert.True(guard.TryAccept("10.0.0.2", out _));
        Assert.False(guard.TryAccept("10.0.0.3", out reason));
        Assert.Contains("Maximum WebSocket", reason, StringComparison.OrdinalIgnoreCase);

        guard.Release("10.0.0.1");
        Assert.Equal(1, guard.ActiveConnections);
    }
}

public class NlSlidingWindowRateLimiterTests
{
    [Fact]
    public void TracksRejectedCount()
    {
        var limiter = new NlSlidingWindowRateLimiter(1);
        Assert.True(limiter.TryAcquire("a"));
        Assert.False(limiter.TryAcquire("a"));
        Assert.True(limiter.RejectedCount >= 1);
    }
}
