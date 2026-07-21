namespace NL.Server.Core.Integration;

/// <summary>Hosted NL session bus connection info for game bridges (Phase B).</summary>
public sealed class NlSessionBusInfo
{
    public required string SessionId { get; init; }
    public required string Token { get; init; }
    public required string WebSocketUrl { get; init; }
    public required string HttpBaseUrl { get; init; }
    public required int WebSocketPort { get; init; }
    public required int HttpPort { get; init; }

    public string BridgeConnectUrl => $"{WebSocketUrl}?token={Uri.EscapeDataString(Token)}";
}

/// <summary>Default ports for the hosted session bus control plane and bridge socket.</summary>
public static class NlSessionBusDefaults
{
    public const int HttpPort = 27020;
    public const int WebSocketPort = 27021;
    public const string DefaultBindHost = "127.0.0.1";
}
