namespace NL.Server.Core;

/// <summary>
/// The Phase 3 real-game event vocabulary (see docs/NLSERVER_MINECRAFT.md), derived from
/// parsing a running Minecraft Java server's console log. This replaces
/// <c>MockGameEventSource</c>'s fixed mock list with real events from a real, simple,
/// moddable multiplayer target, per ROADMAP.md Phase 3.
/// </summary>
public enum MinecraftEventKind
{
    Unknown,
    ServerStarted,
    PlayerJoin,
    PlayerLeave,
    PlayerChat,
    PlayerDeath,
    PlayerAdvancement,
    /// <summary>Inferred or logged respawn (player is alive again after death).</summary>
    PlayerRespawn,
}
