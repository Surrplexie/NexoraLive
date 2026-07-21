using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using NL.Server.Core;
using NL.Server.Core.Integration;

namespace NL.Server.Integration;

/// <summary>TCP client that sends one UTF-8 NDJSON line per action.</summary>
public sealed class NlTcpClientActionChannel : INlActionChannel, IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private NlTcpClientActionChannel(TcpClient client, NetworkStream stream)
    {
        _client = client;
        _stream = stream;
    }

    public static async Task<NlTcpClientActionChannel> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);
        return new NlTcpClientActionChannel(client, client.GetStream());
    }

    public async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            await _stream.WriteAsync(bytes, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _sendLock.Dispose();
        _stream.Dispose();
        _client.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>NL.Server listens; game bridges connect and push NDJSON event lines.</summary>
public sealed class NlTcpListenerEventSource : IGameEventSource, IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Channel<string> _lines;
    private readonly CancellationTokenSource _acceptLoopCts = new();
    private readonly Task _acceptLoop;
    private readonly Action<string>? _log;

    public int Port { get; }

    public NlTcpListenerEventSource(string host, int port, Action<string>? log = null)
    {
        _log = log;
        _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var address = host is "localhost" or "*" or "+" or "0.0.0.0"
            ? IPAddress.Loopback
            : IPAddress.Parse(host);
        _listener = new TcpListener(address, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _log?.Invoke($"[nl tcp] listening on {address}:{Port}");
        _acceptLoop = AcceptLoopAsync(_acceptLoopCts.Token);
    }

    public IGameEventSource CreateEventSource() =>
        LineStreamEventSource.GenericJson(ReadLinesAsync());

    private async IAsyncEnumerable<string> ReadLinesAsync()
    {
        await foreach (var line in _lines.Reader.ReadAllAsync())
        {
            yield return line;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "?";
        _log?.Invoke($"[nl tcp] bridge connected from {remote}");
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                await _lines.Writer.WriteAsync(line, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[nl tcp] client error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
            _log?.Invoke($"[nl tcp] bridge disconnected ({remote})");
        }
    }

    public async IAsyncEnumerable<SessionEvent> ReadEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in CreateEventSource().ReadEventsAsync(cancellationToken))
        {
            yield return evt;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _acceptLoopCts.Cancel();
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

        _acceptLoopCts.Dispose();
    }
}
