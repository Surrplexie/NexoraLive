namespace NL.Server.Core;

/// <summary>
/// Packet type field values for the Source/Valve RCON protocol, which Minecraft's server
/// implements verbatim. Note <see cref="ExecCommand"/> and <see cref="AuthResponse"/> share the
/// same wire value (2) — direction/context (not the type field) tells them apart, matching the
/// real protocol spec.
/// </summary>
public enum RconPacketType
{
    ResponseValue = 0,
    AuthResponse = 2,
    ExecCommand = 2,
    Auth = 3,
}
