namespace NL.Server.Core;

/// <summary>
/// Produces <see cref="SessionEvent"/>s from a real (or recorded) game server. This is the
/// seam that makes Phase 3 game-agnostic: Minecraft log tails, NDJSON pipes from any engine,
/// and future plugin/SDK sources all implement this one interface and feed the same
/// <see cref="NlServerHost"/> / <see cref="NL.Core.RuleEngine"/> pipeline.
/// </summary>
public interface IGameEventSource
{
    IAsyncEnumerable<SessionEvent> ReadEventsAsync(CancellationToken cancellationToken);
}
