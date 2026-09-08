using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonFleetMetricsStore : IFleetMetricsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _lock = new();
    private FleetMetricsState _state = new();

    public JsonFleetMetricsStore(string? path = null)
    {
        _path = path ?? NlFleetPaths.Metrics;
        Load();
    }

    private readonly string _path;

    public void RecordAdmit(bool allowed, string? streamerId = null)
    {
        lock (_lock)
        {
            _state.TotalAdmits++;
            if (!allowed)
            {
                _state.TotalAdmitDenials++;
            }

            _state.RecentAdmits.Add(new TimestampedEvent(DateTimeOffset.UtcNow, streamerId));
            TrimEvents(_state.RecentAdmits, TimeSpan.FromHours(1));
            _state.RecentForkCreates ??= [];
            Persist();
        }
    }

    public void RecordForkCreate(string streamerId, string regionId)
    {
        lock (_lock)
        {
            _state.TotalForkCreates++;
            _state.RecentForkCreates.Add(new ForkCreateEvent(DateTimeOffset.UtcNow, streamerId, regionId));
            TrimForkCreates();
            Persist();
        }
    }

    public void RecordForkCreateLatency(double elapsedMs)
    {
        lock (_lock)
        {
            _state.RecentForkCreateLatenciesMs ??= [];
            _state.RecentForkCreateLatenciesMs.Add(elapsedMs);
            if (_state.RecentForkCreateLatenciesMs.Count > 500)
            {
                _state.RecentForkCreateLatenciesMs.RemoveRange(0, _state.RecentForkCreateLatenciesMs.Count - 500);
            }

            Persist();
        }
    }

    public double GetForkCreateP99Ms()
    {
        lock (_lock)
        {
            var latencies = _state.RecentForkCreateLatenciesMs;
            if (latencies is null || latencies.Count == 0)
            {
                return 0;
            }

            var sorted = latencies.OrderBy(x => x).ToList();
            var idx = (int)Math.Ceiling(sorted.Count * 0.99) - 1;
            return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
        }
    }

    public void RecordDecision(int count = 1)
    {
        lock (_lock)
        {
            _state.TotalDecisions += count;
            Persist();
        }
    }

    public void RecordSessionSample(FleetSessionMetricSample sample)
    {
        lock (_lock)
        {
            _state.RecentSessions.Insert(0, sample);
            if (_state.RecentSessions.Count > 100)
            {
                _state.RecentSessions.RemoveRange(100, _state.RecentSessions.Count - 100);
            }

            Persist();
        }
    }

    public FleetObservabilitySnapshot BuildSnapshot(int activeForks, int activeNls, int recentSessionLimit = 20)
    {
        lock (_lock)
        {
            var forkRate = _state.RecentForkCreates.Count(e => e.AtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
            return new FleetObservabilitySnapshot(
                activeForks,
                activeNls,
                _state.TotalAdmits,
                _state.TotalAdmitDenials,
                _state.TotalDecisions,
                forkRate,
                _state.RecentSessions.Take(recentSessionLimit).ToList(),
                DateTimeOffset.UtcNow);
        }
    }

    public int GetForkCreatesInLastMinute()
    {
        lock (_lock)
        {
            return _state.RecentForkCreates.Count(e => e.AtUtc > DateTimeOffset.UtcNow.AddMinutes(-1));
        }
    }

    public int GetForkCreatesForStreamerInLastHour(string streamerId)
    {
        lock (_lock)
        {
            return _state.RecentForkCreates.Count(e =>
                e.AtUtc > DateTimeOffset.UtcNow.AddHours(-1)
                && string.Equals(e.StreamerId, streamerId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void TrimForkCreates()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        _state.RecentForkCreates.RemoveAll(e => e.AtUtc < cutoff);
    }

    private static void TrimEvents(List<TimestampedEvent> events, TimeSpan window)
    {
        var cutoff = DateTimeOffset.UtcNow - window;
        events.RemoveAll(e => e.AtUtc < cutoff);
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var json = File.ReadAllText(_path);
        _state = JsonSerializer.Deserialize<FleetMetricsState>(json, JsonOptions) ?? new FleetMetricsState();
        _state.RecentForkCreates ??= [];
        _state.RecentAdmits ??= [];
        _state.RecentSessions ??= [];
        _state.RecentForkCreateLatenciesMs ??= [];
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_state, JsonOptions));
    }

    private sealed class FleetMetricsState
    {
        public int TotalAdmits { get; set; }

        public int TotalAdmitDenials { get; set; }

        public int TotalDecisions { get; set; }

        public int TotalForkCreates { get; set; }

        public List<ForkCreateEvent> RecentForkCreates { get; set; } = [];

        public List<TimestampedEvent> RecentAdmits { get; set; } = [];

        public List<FleetSessionMetricSample> RecentSessions { get; set; } = [];

        public List<double> RecentForkCreateLatenciesMs { get; set; } = [];
    }

    private sealed record TimestampedEvent(DateTimeOffset AtUtc, string? StreamerId);

    private sealed record ForkCreateEvent(DateTimeOffset AtUtc, string StreamerId, string RegionId);
}
