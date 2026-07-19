using System.Net.Sockets;
using NL.Server.Core;

namespace NL.Server;

/// <summary>
/// Minimal real Source/Valve RCON client over TCP, used to send real commands (kick/tell/ban)
/// to a Minecraft server in response to a `RuleEngine` decision. Wire framing is handled by
/// <see cref="RconPacket"/> in `NL.Server.Core`; this class only owns the socket and the
/// request-id/response bookkeeping.
/// </summary>
public sealed class RconClient : IAsyncDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private int _nextRequestId = 1;

    public async Task ConnectAndAuthenticateAsync(string host, int port, string password, CancellationToken cancellationToken)
    {
        await _client.ConnectAsync(host, port, cancellationToken);
        _stream = _client.GetStream();

        var requestId = _nextRequestId++;
        await SendAsync(new RconPacket(requestId, RconPacketType.Auth, password), cancellationToken);
        var response = await ReceiveAsync(cancellationToken);

        // Failed auth comes back with RequestId == -1 per the protocol spec.
        if (response.RequestId == -1)
        {
            throw new InvalidOperationException("RCON authentication failed: incorrect password.");
        }
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        var requestId = _nextRequestId++;
        await SendAsync(new RconPacket(requestId, RconPacketType.ExecCommand, command), cancellationToken);
        var response = await ReceiveAsync(cancellationToken);
        return response.Body;
    }

    private async Task SendAsync(RconPacket packet, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Not connected — call ConnectAndAuthenticateAsync first.");
        }

        var bytes = packet.Encode();
        await _stream.WriteAsync(bytes, cancellationToken);
    }

    private async Task<RconPacket> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Not connected — call ConnectAndAuthenticateAsync first.");
        }

        var buffer = new byte[4096];
        var totalRead = 0;

        while (true)
        {
            if (RconPacket.TryDecode(buffer.AsSpan(0, totalRead), out var packet, out _))
            {
                return packet!;
            }

            var read = await _stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (read == 0)
            {
                throw new InvalidOperationException("RCON connection closed before a full response was received.");
            }

            totalRead += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
        }

        _client.Dispose();
    }
}
