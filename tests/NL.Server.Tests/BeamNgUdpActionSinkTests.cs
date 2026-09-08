using NL.Core;
using NL.Server;
using NL.Server.Core;
using Xunit;

namespace NL.Server.Tests;

public class BeamNgUdpActionSinkTests
{
    [Fact]
    public void FormatPacket_IncludesMagicAndFields()
    {
        var packet = BeamNgUdpActionSink.FormatPacket("warn", "Driver", "slow down");
        Assert.StartsWith(BeamNgUdpActionSink.Magic, packet);
        Assert.Contains("warn|Driver|slow down", packet);
    }

    [Theory]
    [InlineData("playerJoin", "kick")]
    [InlineData("crash", "recover")]
    [InlineData("rollover", "recover")]
    [InlineData("leaveBoundary", "recover")]
    [InlineData("airtime", "recover")]
    [InlineData("anomalyTeleport", "recover")]
    [InlineData("move", "warn")]
    public void ChooseAction_MapsEventToCommand(string eventName, string expected)
    {
        var session = new SessionEvent(GameEvent.Simple(eventName), "Driver");
        Assert.Equal(expected, BeamNgUdpActionSink.ChooseAction(session));
    }

    [Fact]
    public void TryParseEndpoint_AcceptsHostPort()
    {
        Assert.True(BeamNgUdpActionSink.TryParseEndpoint("127.0.0.1:27022", out var host, out var port));
        Assert.Equal("127.0.0.1", host);
        Assert.Equal(27022, port);
    }
}
