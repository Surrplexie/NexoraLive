using NL.Core;

namespace NL.Server.Core;

/// <summary>
/// Applies a <see cref="RuleEngine"/> decision back to the running game (kick, tell, mute,
/// custom script, etc.). Dry-run / log-only sinks exist so any game can be validated without
/// a live action channel.
/// </summary>
public interface IGameActionSink : IAsyncDisposable
{
    Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken);
}
