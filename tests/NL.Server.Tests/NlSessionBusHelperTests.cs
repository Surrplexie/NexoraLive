using NL.Server;
using NL.Server.Core.Integration;
using Xunit;

namespace NL.Server.Tests;

public class NlSessionBusHelperTests
{
    [Fact]
    public void ApplyBusSource_SetsWebSocketAndAutoAction()
    {
        var bus = NlSessionBusHelper.CreateBusInfo("127.0.0.1", 27020, 27021, "secret", "sess1");
        var profile = new SessionProfileFile { ConfigPath = "x.nle", SourcePath = "old.log" };

        NlSessionBusHelper.ApplyBusSource(profile, bus);

        Assert.Equal("generic", profile.Game);
        Assert.Equal(bus.WebSocketUrl, profile.SourcePath);
        Assert.Equal("auto", profile.NlActionEndpoint);
        Assert.True(profile.UseSessionBus);
        Assert.Equal("secret", profile.BusToken);
        Assert.Null(profile.RconEndpoint);
    }

    [Fact]
    public void IsNetworkSource_RecognizesTcpAndWs()
    {
        Assert.True(NlSessionBusHelper.IsNetworkSource("tcp://127.0.0.1:27021"));
        Assert.True(NlSessionBusHelper.IsNetworkSource("ws://127.0.0.1:27021/nl/v1"));
        Assert.False(NlSessionBusHelper.IsNetworkSource(@"C:\logs\game.ndjson"));
    }

    [Fact]
    public void BridgeConnectUrl_IncludesEscapedToken()
    {
        var bus = NlSessionBusHelper.CreateBusInfo("127.0.0.1", 27020, 27021, "a+b/c", "s");
        Assert.Contains("token=a%2Bb%2Fc", bus.BridgeConnectUrl);
    }
}
