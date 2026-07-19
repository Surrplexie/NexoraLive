namespace NL.Server.Core;

/// <summary>In-memory event source for tests and scripted demos.</summary>
public sealed class ArrayEventSource : IGameEventSource
{
    private readonly IReadOnlyList<SessionEvent> _events;

    public ArrayEventSource(IEnumerable<SessionEvent> events) => _events = events.ToList();

    public async IAsyncEnumerable<SessionEvent> ReadEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var evt in _events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
            await Task.Yield();
        }
    }
}
