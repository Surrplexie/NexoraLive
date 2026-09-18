namespace NL.Server.Core.Integration;

/// <summary>
/// In-process NDJSON inject for spectator/demo triggers.
/// Must not open a second WebSocket — that steals the live game bridge and logs a disconnect.
/// </summary>
public interface ISessionEventLineInjector
{
    bool TryInjectLine(string line);
}
