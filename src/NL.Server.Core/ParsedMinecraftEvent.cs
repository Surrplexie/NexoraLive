namespace NL.Server.Core;

/// <summary>One parsed line from a Minecraft server's console/log file.</summary>
public sealed record ParsedMinecraftEvent(
    MinecraftEventKind Kind,
    string? PlayerName = null,
    string? ChatMessage = null,
    string? DeathCause = null,
    string? AdvancementTitle = null);
