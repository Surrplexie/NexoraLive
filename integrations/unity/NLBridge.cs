// NL Game Integration Spec v1 — Unity C# stub.
// Attach to a session manager; call NlBridge.Emit from gameplay code.

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public sealed class NlBridge : IDisposable
{
    private ClientWebSocket? _ws;
    private readonly Uri _url;

    public NlBridge(string url = "ws://127.0.0.1:27021/nl/v1") => _url = new Uri(url);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(_url, cancellationToken);
        _ = Task.Run(() => ReadActionsAsync(cancellationToken), cancellationToken);
    }

    public async Task EmitAsync(string eventName, string player, object? props = null, CancellationToken cancellationToken = default)
    {
        if (_ws is null) throw new InvalidOperationException("Not connected");
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var json = props is null
            ? $"{{\"nl\":1,\"event\":\"{eventName}\",\"player\":\"{player}\",\"ts\":{ts}}}"
            : $"{{\"nl\":1,\"event\":\"{eventName}\",\"player\":\"{player}\",\"ts\":{ts},\"props\":{System.Text.Json.JsonSerializer.Serialize(props)}}}";
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private async Task ReadActionsAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (_ws?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await _ws.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) break;
            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            UnityEngine.Debug.Log($"[nl action] {text}");
            // TODO: dispatch warn/kick/recover to your game systems
        }
    }

    public void Dispose() => _ws?.Dispose();
}
