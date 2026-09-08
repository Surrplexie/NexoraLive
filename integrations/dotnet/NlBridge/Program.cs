using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

/// <summary>
/// NL Game Integration Spec v1 — reference bridge (.NET 8 WebSocket).
/// dotnet run --project integrations/dotnet/NlBridge -- --url ws://127.0.0.1:27021/nl/v1?token=TOKEN --sample
/// </summary>
var url = GetArg("--url") ?? "ws://127.0.0.1:27021/nl/v1";
var sample = args.Contains("--sample");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var socket = new ClientWebSocket();
await socket.ConnectAsync(new Uri(url), cts.Token);
Console.WriteLine($"[nl bridge] connected {url}");

var receiveTask = Task.Run(async () =>
{
    var buffer = new byte[8192];
    while (socket.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
    {
        var result = await socket.ReceiveAsync(buffer, cts.Token);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            break;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                Console.WriteLine($"[nl action] {trimmed}");
            }
        }
    }
}, cts.Token);

if (sample)
{
    await EmitAsync(socket, "sessionStart", "Alice", new Dictionary<string, object?> { ["map.id"] = 1 }, cts.Token);
    await EmitAsync(socket, "playerJoin", "Alice", new Dictionary<string, object?> { ["player.alive"] = 1 }, cts.Token);
    await EmitAsync(socket, "shoot", "Alice", new Dictionary<string, object?> { ["weapon.damage"] = 12 }, cts.Token);
    await EmitAsync(socket, "shoot", "Bob", new Dictionary<string, object?> { ["weapon.damage"] = 50 }, cts.Token);
    await Task.Delay(500, cts.Token);
}
else
{
    while (!cts.Token.IsCancellationRequested && socket.State == WebSocketState.Open)
    {
        var line = Console.ReadLine();
        if (line is null)
        {
            break;
        }

        var trimmed = line.Trim();
        if (trimmed.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(trimmed);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token);
        }
    }
}

await receiveTask;
return;

static string? GetArg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static async Task EmitAsync(
    ClientWebSocket socket,
    string eventName,
    string player,
    Dictionary<string, object?>? props,
    CancellationToken cancellationToken)
{
    var payload = new Dictionary<string, object?>
    {
        ["nl"] = 1,
        ["event"] = eventName,
        ["player"] = player,
        ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    };
    if (props is not null)
    {
        payload["props"] = props;
    }

    var json = JsonSerializer.Serialize(payload);
    var bytes = Encoding.UTF8.GetBytes(json);
    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
}
