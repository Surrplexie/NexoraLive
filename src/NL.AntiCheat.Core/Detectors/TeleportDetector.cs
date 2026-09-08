using NL.Server.Core;

namespace NL.AntiCheat.Core.Detectors;

/// <summary>
/// Watches consecutive <c>move</c> (or any event carrying <c>player.x</c>/<c>player.z</c>)
/// positions and flags one-step jumps that exceed a distance or speed threshold — the
/// "impossible locomotion" signal for teleport / speed hacks.
/// </summary>
public sealed class TeleportDetector : IAnomalyDetector
{
    private readonly AnomalyThresholds _thresholds;
    private readonly Dictionary<string, (double X, double Y, double Z, DateTimeOffset At)> _last = new(StringComparer.OrdinalIgnoreCase);

    public TeleportDetector(AnomalyThresholds? thresholds = null) =>
        _thresholds = thresholds ?? AnomalyThresholds.Default;

    public IReadOnlyList<AnomalySignal> Observe(SessionEvent sessionEvent, DateTimeOffset nowUtc)
    {
        var player = sessionEvent.PlayerName;
        if (player is null)
        {
            return Array.Empty<AnomalySignal>();
        }

        var props = sessionEvent.Event.Properties;
        if (!props.TryGetValue("player.x", out var x) || !props.TryGetValue("player.z", out var z))
        {
            return Array.Empty<AnomalySignal>();
        }

        props.TryGetValue("player.y", out var y);

        if (!_last.TryGetValue(player, out var prev))
        {
            _last[player] = (x, y, z, nowUtc);
            return Array.Empty<AnomalySignal>();
        }

        var dx = x - prev.X;
        var dy = y - prev.Y;
        var dz = z - prev.Z;
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var deltaMs = Math.Max(1, (nowUtc - prev.At).TotalMilliseconds);
        var speed = distance / (deltaMs / 1000.0);

        _last[player] = (x, y, z, nowUtc);

        var overDistance = distance > _thresholds.TeleportMaxDistance;
        var overSpeed = speed > _thresholds.TeleportMaxSpeedUnitsPerSec;
        if (!overDistance && !overSpeed)
        {
            return Array.Empty<AnomalySignal>();
        }

        return new[]
        {
            new AnomalySignal(
                AnomalyKind.Teleport,
                player,
                sessionEvent.Event.Name,
                $"position jump {distance:F1} units in {deltaMs:F0}ms (speed {speed:F1}/s).",
                new Dictionary<string, double>
                {
                    ["anomaly.distance"] = distance,
                    ["anomaly.deltaMs"] = deltaMs,
                    ["anomaly.speed"] = speed,
                    ["anomaly.maxDistance"] = _thresholds.TeleportMaxDistance,
                    ["anomaly.maxSpeed"] = _thresholds.TeleportMaxSpeedUnitsPerSec,
                },
                nowUtc),
        };
    }
}
