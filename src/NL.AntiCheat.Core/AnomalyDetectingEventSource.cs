using System.Runtime.CompilerServices;
using NL.Server.Core;

namespace NL.AntiCheat.Core;

/// <summary>
/// Decorates any <see cref="IGameEventSource"/> so that after each real game event is
/// yielded, any Phase 5 anomaly signals are also yielded as ordinary <see cref="SessionEvent"/>s
/// (with well-known <c>anomaly*</c> event names). <see cref="NlServerHost"/> /
/// <see cref="NL.Core.RuleEngine"/> need no awareness that those events came from detectors.
/// </summary>
public sealed class AnomalyDetectingEventSource : IGameEventSource
{
    private readonly IGameEventSource _inner;
    private readonly AnomalyPipeline _pipeline;

    public AnomalyDetectingEventSource(IGameEventSource inner, AnomalyPipeline? pipeline = null)
    {
        _inner = inner;
        _pipeline = pipeline ?? AnomalyPipeline.CreateDefault();
    }

    public async IAsyncEnumerable<SessionEvent> ReadEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var sessionEvent in _inner.ReadEventsAsync(cancellationToken))
        {
            yield return sessionEvent;

            foreach (var signal in _pipeline.Observe(sessionEvent))
            {
                yield return AnomalyEventMapper.ToSessionEvent(signal);
            }
        }
    }
}
