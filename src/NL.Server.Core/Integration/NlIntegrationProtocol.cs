namespace NL.Server.Core.Integration;

/// <summary>NL Game Integration Protocol v1 constants. See docs/NL_INTEGRATION_SPEC.md.</summary>
public static class NlIntegrationProtocol
{
    public const int Version = 1;

    /// <summary>Default TCP/WS listen port for game bridges (NL.Server --source tcp/ws://…).</summary>
    public const int DefaultEventPort = 27021;

    /// <summary>Default TCP port when the bridge listens for outbound actions (--nl-action tcp://…).</summary>
    public const int DefaultActionPort = 27023;

    /// <summary>HTTP path segment for WebSocket upgrade (ws://host:port/nl/v1).</summary>
    public const string WebSocketPath = "/nl/v1";

    public static readonly string[] StandardActions =
    [
        "warn", "kick", "recover", "tell", "mute", "despawn", "custom",
    ];
}
