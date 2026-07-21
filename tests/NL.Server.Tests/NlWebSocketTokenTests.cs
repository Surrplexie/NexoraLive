using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using NL.Server;
using NL.Server.Integration;
using Xunit;

namespace NL.Server.Tests;

public class NlWebSocketTokenTests
{
    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public async Task BadToken_RejectsUpgrade()
    {
        var port = GetFreePort();
        await using var listener = new NlWebSocketListenerEventSource(
            "127.0.0.1",
            port,
            validateToken: token => token == "good");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new ClientWebSocket();

        var uri = new Uri($"ws://127.0.0.1:{listener.Port}/nl/v1?token=bad");
        await Assert.ThrowsAnyAsync<Exception>(() => client.ConnectAsync(uri, cts.Token));
    }

    [Fact]
    public async Task GoodToken_AcceptsEvents()
    {
        var port = GetFreePort();
        await using var listener = new NlWebSocketListenerEventSource(
            "127.0.0.1",
            port,
            validateToken: token => token == "good");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in listener.ReadEventsAsync(cts.Token))
            {
                Assert.Equal("shoot", evt.Event.Name);
                cts.Cancel();
                return;
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);
        using var client = new ClientWebSocket();
        await client.ConnectAsync(
            new Uri($"ws://127.0.0.1:{listener.Port}/nl/v1?token=good"),
            cts.Token);

        var line = """{"nl":1,"event":"shoot","player":"Alice"}""" + "\n";
        await client.SendAsync(Encoding.UTF8.GetBytes(line), WebSocketMessageType.Text, true, cts.Token);

        await readTask;
    }
}
