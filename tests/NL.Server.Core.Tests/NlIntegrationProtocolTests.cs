using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Generic;
using NL.Server.Core.Integration;
using Xunit;

namespace NL.Server.Core.Tests;

public class NlIntegrationProtocolTests
{
    [Fact]
    public void TryParse_NlVersion1_Accepted()
    {
        var session = GenericJsonLineParser.TryParse(
            """{"nl":1,"event":"shoot","player":"Alice"}""");
        Assert.Equal("shoot", session!.Event.Name);
    }

    [Fact]
    public void TryParse_UnsupportedNlVersion_Throws()
    {
        Assert.Throws<FormatException>(() =>
            GenericJsonLineParser.TryParse("""{"nl":99,"event":"shoot"}"""));
    }

    [Fact]
    public void NlSourceUri_FilePath_Recognized()
    {
        Assert.True(NlSourceUri.TryParse(@"C:\data\events.ndjson", out var uri));
        Assert.Equal(NlSourceKind.File, uri!.Kind);
    }

    [Fact]
    public void NlSourceUri_TcpListen_Recognized()
    {
        Assert.True(NlSourceUri.TryParse("tcp://127.0.0.1:27021", out var uri));
        Assert.Equal(NlSourceKind.TcpListen, uri!.Kind);
        Assert.Equal(27021, uri.Port);
    }

    [Fact]
    public void NlSourceUri_WebSocketListen_Recognized()
    {
        Assert.True(NlSourceUri.TryParse("ws://127.0.0.1:27021/nl/v1", out var uri));
        Assert.Equal(NlSourceKind.WebSocketListen, uri!.Kind);
    }

    [Fact]
    public void NlActionEnvelope_SerializesStandardFields()
    {
        var line = NlActionEnvelope.Serialize(
            new SessionEvent(GameEvent.Simple("shoot"), "Alice"),
            ActionResult.Block("Too strong"));

        Assert.Contains("\"nl\":1", line);
        Assert.Contains("\"type\":\"action\"", line);
        Assert.Contains("\"action\":\"warn\"", line);
        Assert.Contains("\"player\":\"Alice\"", line);
        Assert.Contains("\"event\":\"shoot\"", line);
    }

    [Fact]
    public void NlStandardActions_PlayerJoin_MapsKick()
    {
        var action = NlStandardActions.ChooseAction(
            new SessionEvent(GameEvent.Simple("playerJoin"), "Eve"));
        Assert.Equal("kick", action);
    }
}
