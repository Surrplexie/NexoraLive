using NL.Server.Core;

namespace NL.AntiCheat.Core;

/// <summary>
/// Runs every registered <see cref="IAnomalyDetector"/> against each session event and
/// returns the combined signal list. Factories
/// <see cref="CreateDefault"/> wire the three built-in detectors (impossible action, rate
/// spike, teleport) with shared <see cref="AnomalyThresholds"/>.
/// </summary>
public sealed class AnomalyPipeline
{
    private readonly IReadOnlyList<IAnomalyDetector> _detectors;
    private readonly Func<DateTimeOffset> _clock;

    public AnomalyPipeline(IEnumerable<IAnomalyDetector> detectors, Func<DateTimeOffset>? clock = null)
    {
        _detectors = detectors.ToList();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public static AnomalyPipeline CreateDefault(
        AnomalyThresholds? thresholds = null, Func<DateTimeOffset>? clock = null)
    {
        var t = thresholds ?? AnomalyThresholds.Default;
        return new AnomalyPipeline(
            new IAnomalyDetector[]
            {
                new Detectors.ImpossibleActionDetector(),
                new Detectors.RateSpikeDetector(t),
                new Detectors.TeleportDetector(t),
            },
            clock);
    }

    public IReadOnlyList<AnomalySignal> Observe(SessionEvent sessionEvent)
    {
        var now = sessionEvent.TimestampUtc ?? _clock();
        var signals = new List<AnomalySignal>();
        foreach (var detector in _detectors)
        {
            signals.AddRange(detector.Observe(sessionEvent, now));
        }

        return signals;
    }
}
