using NL.Server.Core.Integration;

namespace NL.Server;

/// <summary>Applies hosted session bus defaults to profiles and session options.</summary>
public static class NlSessionBusHelper
{
    public static void ApplyBusSource(SessionProfileFile profile, NlSessionBusInfo bus)
    {
        profile.Game = "generic";
        profile.SourcePath = bus.WebSocketUrl;
        profile.NlActionEndpoint = "auto";
        profile.UseSessionBus = true;
        profile.BusToken = bus.Token;
        profile.BeamngCommandEndpoint = null;
        profile.RconEndpoint = null;
    }

    public static NlSessionBusInfo CreateBusInfo(string bindHost, int httpPort, int wsPort, string token, string sessionId)
    {
        var wsUrl = $"ws://{bindHost}:{wsPort}{NlIntegrationProtocol.WebSocketPath}";
        return new NlSessionBusInfo
        {
            SessionId = sessionId,
            Token = token,
            WebSocketUrl = wsUrl,
            HttpBaseUrl = $"http://{bindHost}:{httpPort}",
            WebSocketPort = wsPort,
            HttpPort = httpPort,
        };
    }

    public static bool IsNetworkSource(string? sourcePath) =>
        NlSourceUri.TryParse(sourcePath, out var uri)
        && uri is not null
        && uri.Kind is NlSourceKind.TcpListen or NlSourceKind.WebSocketListen;
}
