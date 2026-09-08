using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NL.Fork.Core;

/// <summary>
/// TCP connect listener for <c>rimworld://host:port</c> (Together-style dedicated handshake).
/// Real RimWorld Together uses the licensed dedicated binary; this banner keeps the
/// orchestrator port mapped and lets NL Client probe readiness without shipping the game.
/// </summary>
public sealed class RimWorldConnectListener : IAsyncDisposable
{
    public const string HandshakeBanner = "NL-RIMWORLD/1 together-ready\n";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<TcpClient> _clients = [];
    private readonly object _gate = new();
    private Task? _acceptLoop;

    public RimWorldConnectListener(int port, IPAddress? address = null)
    {
        Port = port;
        _listener = new TcpListener(address ?? IPAddress.Any, port);
    }

    public int Port { get; }

    public int BoundPort { get; private set; }

    public int ConnectedClients
    {
        get
        {
            lock (_gate)
            {
                return _clients.Count(c => c.Connected);
            }
        }
    }

    public bool IsListening { get; private set; }

    public void Start()
    {
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        IsListening = true;
        _acceptLoop = AcceptLoopAsync(_cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        IsListening = false;
        try
        {
            _listener.Stop();
        }
        catch
        {
            // already stopped
        }

        lock (_gate)
        {
            foreach (var client in _clients)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    // ignore
                }
            }

            _clients.Clear();
        }

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected
            }
        }

        _cts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            lock (_gate)
            {
                _clients.Add(client);
            }

            _ = Task.Run(() => ServeClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private static async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            var stream = client.GetStream();
            var banner = Encoding.ASCII.GetBytes(HandshakeBanner);
            await stream.WriteAsync(banner, cancellationToken).ConfigureAwait(false);
            var buffer = new byte[64];
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
            }
        }
        catch
        {
            // client dropped
        }
        finally
        {
            try
            {
                client.Close();
            }
            catch
            {
                // ignore
            }
        }
    }
}
