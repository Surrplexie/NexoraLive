using System.Net.Sockets;
using System.Text;
using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Integration;
using NL.Server.Integration;
using Xunit;

namespace NL.Server.Tests;

public class NlTcpIntegrationTests
{
    [Fact]
    public async Task TcpRoundTrip_EmitsSessionEvent()
    {
        await using var listener = new NlTcpListenerEventSource("127.0.0.1", 0);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in listener.ReadEventsAsync(cts.Token))
            {
                Assert.Equal("shoot", evt.Event.Name);
                Assert.Equal("Alice", evt.PlayerName);
                cts.Cancel();
                return;
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", listener.Port, cts.Token);
        var line = """{"nl":1,"event":"shoot","player":"Alice","props":{"weapon.damage":1}}""";
        await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), cts.Token);

        await readTask;
    }

    [Fact]
    public void NlActionEnvelope_Crash_MapsRecoverAction()
    {
        var json = NlActionEnvelope.Serialize(
            new SessionEvent(GameEvent.Simple("crash"), "Driver"),
            ActionResult.Block("hard crash"));
        Assert.Contains("\"decision\":\"Block\"", json);
        Assert.Contains("\"action\":\"recover\"", json);
    }
}
