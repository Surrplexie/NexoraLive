using NL.Core;

namespace NL.Server.Core;

/// <summary>Records every Apply call — dry-run sink used by tests and <c>--replay</c> demos.</summary>
public sealed class RecordingActionSink : IGameActionSink
{
    public List<(SessionEvent Session, ActionResult Result)> Applied { get; } = new();

    public Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken)
    {
        Applied.Add((sessionEvent, result));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>No-op sink for dry-run sessions that still print decisions to the console elsewhere.</summary>
public sealed class NullActionSink : IGameActionSink
{
    public static readonly NullActionSink Instance = new();

    public Task ApplyAsync(SessionEvent sessionEvent, ActionResult result, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
