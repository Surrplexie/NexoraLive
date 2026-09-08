using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using NL.Server;
using NL.Server.Core;
using NL.Server.Core.Integration;

namespace NL.Server.Integration;

/// <summary>Bidirectional WebSocket session: bridge sends event lines, NL sends action lines.</summary>
public sealed class NlWebSocketSession : INlActionChannel
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public NlWebSocketSession(WebSocket socket) => _socket = socket;

    public async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            return;
        }

        var payload = Encoding.UTF8.GetBytes(line);
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}

/// <summary>NL.Server listens for WebSocket upgrades at <see cref="NlIntegrationProtocol.WebSocketPath"/>.</summary>
public sealed class NlWebSocketListenerEventSource : IGameEventSource, IAsyncDisposable, IActiveActionChannelProvider
{
    private readonly HttpListener _listener;
    private readonly Channel<string> _lines;
    private readonly CancellationTokenSource _loopCts = new();
    private readonly Task _acceptLoop;
    private readonly Action<string>? _log;
    private readonly Func<string?, bool>? _validateToken;

    public NlWebSocketSession? ActiveSession { get; private set; }

    public int Port { get; }

    public NlWebSocketListenerEventSource(
        string host,
        int port,
        Action<string>? log = null,
        Func<string?, bool>? validateToken = null)
    {
        _log = log;
        _validateToken = validateToken;
        _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var bindHost = NlListenHost.ResolveHttpListenerHost(host);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{bindHost}:{port}/");
        _listener.Start();
        Port = port;
        var displayHost = NlListenHost.IsAllInterfaces(host) ? "0.0.0.0" : bindHost;
        _log?.Invoke($"[nl ws] listening on ws://{displayHost}:{port}{NlIntegrationProtocol.WebSocketPath}");
        _acceptLoop = AcceptLoopAsync(_loopCts.Token);
    }

    public INlActionChannel? GetActiveActionChannel() => ActiveSession;

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
                continue;
            }

            if (_validateToken is not null)
            {
                var token = context.Request.QueryString["token"];
                if (!_validateToken(token))
                {
                    _log?.Invoke("[nl ws] rejected bridge (bad token)");
                    context.Response.StatusCode = 401;
                    context.Response.Close();
                    continue;
                }
            }

            var remoteIp = context.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
            var guard = NlWebSocketConnectionGuard.Current;
            if (guard is not null && !guard.TryAccept(remoteIp, out var rejectReason))
            {
                _log?.Invoke($"[nl ws] rejected bridge ({rejectReason}) from {remoteIp}");
                context.Response.StatusCode = 429;
                context.Response.Close();
                continue;
            }

            _ = HandleWebSocketAsync(context, remoteIp, guard, cancellationToken);
        }
    }

    private async Task HandleWebSocketAsync(
        HttpListenerContext context,
        string remoteIp,
        NlWebSocketConnectionGuard? guard,
        CancellationToken cancellationToken)
    {
        WebSocketContext? wsContext = null;
        try
        {
            try
            {
                wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[nl ws] upgrade failed: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
                return;
            }

            var socket = wsContext.WebSocket;
            var session = new NlWebSocketSession(socket);
            ActiveSession = session;
            _log?.Invoke("[nl ws] bridge connected");

            var buffer = new byte[8192];
            var pending = new StringBuilder();
            try
            {
                while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    pending.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    await FlushPendingLinesAsync(pending, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
            catch (WebSocketException ex)
            {
                _log?.Invoke($"[nl ws] socket error: {ex.Message}");
            }
            finally
            {
                if (ActiveSession == session)
                {
                    ActiveSession = null;
                }

                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    }
                    catch
                    {
                        // ignore close races
                    }
                }

                socket.Dispose();
                _log?.Invoke("[nl ws] bridge disconnected");
            }
        }
        finally
        {
            guard?.Release(remoteIp);
        }
    }

    private async Task FlushPendingLinesAsync(StringBuilder pending, CancellationToken cancellationToken)
    {
        while (true)
        {
            var text = pending.ToString();
            var newline = text.IndexOf('\n');
            if (newline < 0)
            {
                return;
            }

            var line = text[..newline].TrimEnd('\r');
            pending.Remove(0, newline + 1);
            if (!string.IsNullOrWhiteSpace(line))
            {
                await _lines.Writer.WriteAsync(line, cancellationToken);
            }
        }
    }

    public async IAsyncEnumerable<SessionEvent> ReadEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var inner = LineStreamEventSource.GenericJson(_lines.Reader.ReadAllAsync(cancellationToken));
        await foreach (var evt in inner.ReadEventsAsync(cancellationToken))
        {
            yield return evt;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _loopCts.Cancel();
        _listener.Stop();
        _lines.Writer.TryComplete();
        try
        {
            await _acceptLoop;
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        _loopCts.Dispose();
    }
}
