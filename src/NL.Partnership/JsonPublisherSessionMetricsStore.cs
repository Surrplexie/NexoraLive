using System.Text.Json;
using NL.Partnership.Core;

namespace NL.Partnership;

public sealed class JsonPublisherSessionMetricsStore : IPublisherSessionMetricsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, int> _publisherJoins = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _gameJoins = new(StringComparer.OrdinalIgnoreCase);

    public JsonPublisherSessionMetricsStore(string? path = null)
    {
        _path = path ?? NlPartnershipPaths.Metrics;
        Load();
    }

    public void RecordJoin(string gameId, string? publisherId = null)
    {
        lock (_lock)
        {
            _gameJoins[gameId] = _gameJoins.GetValueOrDefault(gameId) + 1;
            if (!string.IsNullOrWhiteSpace(publisherId))
            {
                _publisherJoins[publisherId] = _publisherJoins.GetValueOrDefault(publisherId) + 1;
            }

            Persist();
        }
    }

    public int GetJoinCount(string publisherId)
    {
        lock (_lock)
        {
            return _publisherJoins.GetValueOrDefault(publisherId);
        }
    }

    public int GetGameJoinCount(string gameId)
    {
        lock (_lock)
        {
            return _gameJoins.GetValueOrDefault(gameId);
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var json = File.ReadAllText(_path);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("publishers", out var pubs))
        {
            _publisherJoins = pubs.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.OrdinalIgnoreCase);
        }

        if (doc.RootElement.TryGetProperty("games", out var games))
        {
            _gameJoins = games.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetInt32(), StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var payload = new
        {
            publishers = _publisherJoins,
            games = _gameJoins,
            updatedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(payload, JsonOptions));
    }
}
