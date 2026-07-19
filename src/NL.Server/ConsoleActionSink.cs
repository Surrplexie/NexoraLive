using NL.Core;
using NL.Server.Core;

namespace NL.Server;

/// <summary>Prints apply attempts — used when no real action channel is configured.
/// Optional <paramref name="log"/> redirects output (Session Host UI); defaults to Console.</summary>
public sealed class ConsoleActionSink : IGameActionSink
{
    private readonly Action<string> _log;

    public ConsoleActionSink(Action<string>? log = null) =>
        _log = log ?? Console.WriteLine;

    public Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken)
    {
        var player = sessionEvent.PlayerName ?? "?";
        _log($"  [action dry-run] would apply {result.Decision} for {player}/{sessionEvent.Event.Name}: {result.Message ?? "(no message)"}");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
