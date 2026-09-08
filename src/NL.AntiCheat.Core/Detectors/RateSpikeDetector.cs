using NL.Server.Core;

namespace NL.AntiCheat.Core.Detectors;

/// <summary>
/// Flags when one player emits the same event name too many times inside a sliding time
/// window (fire-rate macros, chat floods). Emits at most one spike signal per window trip
/// (re-arms after the window drains below the threshold).
/// </summary>
public sealed class RateSpikeDetector : IAnomalyDetector
{
    private static readonly HashSet<string> IgnoredEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        // Don't rate-limit lifecycle noise or the anomaly events we ourselves emit.
        "playerJoin", "playerLeave", "playerDeath", "death", "respawn",
        "sessionStart", "sessionEnd", "recover",
        AnomalyEventNames.ImpossibleAction, AnomalyEventNames.RateSpike, AnomalyEventNames.Teleport,
    };

    private readonly AnomalyThresholds _thresholds;
    private readonly Dictionary<(string Player, string Event), Queue<DateTimeOffset>> _windows = new();
    private readonly HashSet<(string Player, string Event)> _tripped = new();

    public RateSpikeDetector(AnomalyThresholds? thresholds = null) =>
        _thresholds = thresholds ?? AnomalyThresholds.Default;

    public IReadOnlyList<AnomalySignal> Observe(SessionEvent sessionEvent, DateTimeOffset nowUtc)
    {
        var player = sessionEvent.PlayerName;
        if (player is null || IgnoredEvents.Contains(sessionEvent.Event.Name))
        {
            return Array.Empty<AnomalySignal>();
        }

        var key = (player, sessionEvent.Event.Name);
        if (!_windows.TryGetValue(key, out var queue))
        {
            queue = new Queue<DateTimeOffset>();
            _windows[key] = queue;
        }

        queue.Enqueue(nowUtc);
        var window = TimeSpan.FromMilliseconds(_thresholds.RateSpikeWindowMs);
        while (queue.Count > 0 && nowUtc - queue.Peek() > window)
        {
            queue.Dequeue();
        }

        if (queue.Count < _thresholds.RateSpikeMaxEvents)
        {
            _tripped.Remove(key);
            return Array.Empty<AnomalySignal>();
        }

        if (_tripped.Contains(key))
        {
            return Array.Empty<AnomalySignal>(); // already flagged this spike
        }

        _tripped.Add(key);
        return new[]
        {
            new AnomalySignal(
                AnomalyKind.RateSpike,
                player,
                sessionEvent.Event.Name,
                $"{queue.Count}× {sessionEvent.Event.Name} in {_thresholds.RateSpikeWindowMs}ms (threshold {_thresholds.RateSpikeMaxEvents}).",
                new Dictionary<string, double>
                {
                    ["anomaly.count"] = queue.Count,
                    ["anomaly.windowMs"] = _thresholds.RateSpikeWindowMs,
                    ["anomaly.threshold"] = _thresholds.RateSpikeMaxEvents,
                },
                nowUtc),
        };
    }
}
