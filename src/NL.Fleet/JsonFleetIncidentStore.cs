using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonFleetIncidentStore : IFleetIncidentStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonFleetIncidentStore(string? path = null) => _path = path ?? NlFleetPaths.Incidents;

    public IReadOnlyList<FleetIncident> ListRecent(int count = 50)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        lock (_lock)
        {
            return File.ReadAllLines(_path)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .TakeLast(count)
                .Select(l => System.Text.Json.JsonSerializer.Deserialize<FleetIncident>(l))
                .Where(i => i is not null)
                .Cast<FleetIncident>()
                .Reverse()
                .ToList();
        }
    }

    public void Add(FleetIncident incident)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var line = System.Text.Json.JsonSerializer.Serialize(incident);
        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}

public sealed class JsonFleetStreamerRequirementsStore : IFleetStreamerRequirementsStore
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, FleetStreamerRequirements> _map = new(StringComparer.OrdinalIgnoreCase);

    public JsonFleetStreamerRequirementsStore(string? path = null)
    {
        _path = path ?? NlFleetPaths.StreamerRequirements;
        Load();
    }

    public FleetStreamerRequirements GetOrDefault(string streamerId)
    {
        lock (_lock)
        {
            return _map.TryGetValue(streamerId, out var req)
                ? req
                : new FleetStreamerRequirements(streamerId, MinTwitchFollowers: 0, MinYouTubeSubscribers: 0, false);
        }
    }

    public void Save(FleetStreamerRequirements requirements)
    {
        lock (_lock)
        {
            _map[requirements.StreamerId] = requirements;
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_path, System.Text.Json.JsonSerializer.Serialize(_map.Values.ToList(), JsonOptions));
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var list = System.Text.Json.JsonSerializer.Deserialize<List<FleetStreamerRequirements>>(File.ReadAllText(_path), JsonOptions);
        if (list is null)
        {
            return;
        }

        _map = list.ToDictionary(r => r.StreamerId, StringComparer.OrdinalIgnoreCase);
    }
}
