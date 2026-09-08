using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Generic;
using NL.Server.Core.Integration;

namespace NL.Fork.Core;

/// <summary>WebSocket client: fork emits events, receives NL action lines (integration spec v1).</summary>
public sealed class ForkNlBridgeClient : IForkDecisionSink, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly TimeSpan _actionWait;
    private readonly Action<string>? _log;
    private readonly StringBuilder _receiveBuffer = new();
    private readonly object _actionLock = new();
    private readonly Queue<NlActionMessage> _pendingActions = new();
    private Task? _receiveLoop;
    private CancellationTokenSource? _receiveCts;

    public ForkNlBridgeClient(TimeSpan? actionWait = null, Action<string>? log = null)
    {
        _actionWait = actionWait ?? TimeSpan.FromSeconds(3);
        _log = log;
    }

    public bool IsConnected => _socket.State == WebSocketState.Open;

    public async Task ConnectAsync(Uri wsUrl, CancellationToken cancellationToken)
    {
        if (_socket.State == WebSocketState.Open)
        {
            return;
        }

        await _socket.ConnectAsync(wsUrl, cancellationToken);
        _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveLoop = ReceiveLoopAsync(_receiveCts.Token);
        _log?.Invoke($"[fork ws] connected → {wsUrl}");
    }

    public async Task<ForkDecisionOutcome> EvaluateAsync(SessionEvent sessionEvent, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Fork bridge is not connected.");
        }

        var payload = SerializeEvent(sessionEvent);
        await SendLineAsync(payload, cancellationToken);
        _log?.Invoke($"[fork event] {payload.Trim()}");

        var action = await WaitForActionAsync(
            sessionEvent.PlayerName ?? "",
            sessionEvent.Event.Name,
            cancellationToken);

        if (action is null)
        {
            return new ForkDecisionOutcome(Decision.Allow, null, null);
        }

        var decision = string.Equals(action.Decision, "Block", StringComparison.OrdinalIgnoreCase)
            ? Decision.Block
            : Decision.Allow;

        return new ForkDecisionOutcome(decision, action.Message, action);
    }

    private async Task<NlActionMessage?> WaitForActionAsync(
        string player,
        string eventName,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_actionWait);
        var token = timeoutCts.Token;

        while (!token.IsCancellationRequested)
        {
            lock (_actionLock)
            {
                if (_pendingActions.Count > 0)
                {
                    var next = _pendingActions.Dequeue();
                    if (Matches(next, player, eventName))
                    {
                        return next;
                    }

                    _pendingActions.Enqueue(next);
                }
            }

            try
            {
                await Task.Delay(25, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        lock (_actionLock)
        {
            foreach (var queued in _pendingActions)
            {
                if (Matches(queued, player, eventName))
                {
                    return queued;
                }
            }
        }

        return null;
    }

    private static bool Matches(NlActionMessage action, string player, string eventName) =>
        string.Equals(action.Player, player, StringComparison.OrdinalIgnoreCase)
        && string.Equals(action.Event, eventName, StringComparison.OrdinalIgnoreCase);

    private static string SerializeEvent(SessionEvent sessionEvent)
    {
        var propsObj = new Dictionary<string, double>();
        foreach (var (key, value) in sessionEvent.Event.Properties)
        {
            propsObj[key] = value;
        }

        var payload = new Dictionary<string, object?>
        {
            ["nl"] = NlIntegrationProtocol.Version,
            ["event"] = sessionEvent.Event.Name,
            ["player"] = sessionEvent.PlayerName ?? "",
            ["ts"] = (sessionEvent.TimestampUtc ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds(),
        };

        if (propsObj.Count > 0)
        {
            payload["props"] = propsObj;
        }

        return JsonSerializer.Serialize(payload, JsonOptions) + "\n";
    }

    private async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        try
        {
            while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                _receiveBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                FlushActionLines();
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
        catch (WebSocketException ex)
        {
            _log?.Invoke($"[fork ws] receive error: {ex.Message}");
        }
    }

    private void FlushActionLines()
    {
        while (true)
        {
            var text = _receiveBuffer.ToString();
            var newline = text.IndexOf('\n');
            if (newline < 0)
            {
                if (_receiveBuffer.Length > 0 && _receiveBuffer.ToString().TrimStart().StartsWith('{'))
                {
                    var single = _receiveBuffer.ToString().Trim();
                    _receiveBuffer.Clear();
                    TryEnqueueAction(single);
                }

                return;
            }

            var line = text[..newline].TrimEnd('\r');
            _receiveBuffer.Remove(0, newline + 1);
            if (!string.IsNullOrWhiteSpace(line))
            {
                TryEnqueueAction(line);
            }
        }
    }

    private void TryEnqueueAction(string line)
    {
        var action = NlActionEnvelope.TryParse(line);
        if (action is null)
        {
            var evt = GenericJsonLineParser.TryParse(line);
            if (evt is not null)
            {
                return;
            }

            _log?.Invoke($"[fork ws] ignored line: {line}");
            return;
        }

        lock (_actionLock)
        {
            _pendingActions.Enqueue(action);
        }

        _log?.Invoke($"[fork action] {line.Trim()}");
    }

    public async ValueTask DisposeAsync()
    {
        _receiveCts?.Cancel();
        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop;
            }
            catch
            {
                // ignore
            }
        }

        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch
            {
                // ignore
            }
        }

        _socket.Dispose();
        _receiveCts?.Dispose();
        _sendLock.Dispose();
    }
}
