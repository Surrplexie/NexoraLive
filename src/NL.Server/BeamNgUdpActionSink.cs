using System.Net;
using System.Net.Sockets;
using System.Text;
using NL.Core;
using NL.Server.Core;

namespace NL.Server;

/// <summary>
/// Sends Block decisions to the BeamNG NL_BeamNGBridge Lua mod over localhost UDP.
/// Packet format (UTF-8 text):
/// <c>SCBN1\n{action}|{player}|{message}</c>
/// Actions: warn, recover, despawn, kick.
/// </summary>
public sealed class BeamNgUdpActionSink : IGameActionSink
{
    public const string Magic = "SCBN1";
    public const int DefaultPort = NlPaths.BeamngCommandPort;

    private readonly UdpClient _client;
    private readonly IPEndPoint _endpoint;
    private readonly Action<string>? _log;

    public BeamNgUdpActionSink(string host, int port, Action<string>? log = null)
    {
        _endpoint = new IPEndPoint(IPAddress.Parse(host), port);
        _client = new UdpClient();
        _log = log;
    }

    public static bool TryParseEndpoint(string endpoint, out string host, out int port)
    {
        host = "127.0.0.1";
        port = DefaultPort;
        var parts = endpoint.Split(':', 2);
        if (parts.Length == 1)
        {
            return int.TryParse(parts[0], out port);
        }

        host = parts[0];
        return int.TryParse(parts[1], out port) && IPAddress.TryParse(host, out _);
    }

    /// <summary>Chooses warn vs recover/kick from the blocked event name.</summary>
    public static string ChooseAction(SessionEvent sessionEvent)
    {
        var name = sessionEvent.Event.Name;
        if (string.Equals(name, "playerJoin", StringComparison.OrdinalIgnoreCase))
        {
            return "kick";
        }

        if (name.StartsWith("anomaly", StringComparison.OrdinalIgnoreCase)
            || name is "crash" or "rollover" or "leaveBoundary" or "airtime")
        {
            return "recover";
        }

        return "warn";
    }

    public static string FormatPacket(string action, string? player, string? message) =>
        $"{Magic}\n{action}|{player ?? ""}|{message ?? ""}";

    public Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken)
    {
        var action = ChooseAction(sessionEvent);
        var packet = FormatPacket(action, sessionEvent.PlayerName, result.Message);
        var bytes = Encoding.UTF8.GetBytes(packet);
        _client.Send(bytes, bytes.Length, _endpoint);
        _log?.Invoke(
            $"  [beamng udp] {action} → {_endpoint} for {sessionEvent.PlayerName ?? "?"}/{sessionEvent.Event.Name}: {result.Message ?? ""}");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
