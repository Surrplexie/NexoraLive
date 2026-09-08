using System.Text.Json;
using NL.Partnership.Core;

namespace NL.Partnership;

public sealed class JsonAtOwnRiskAcknowledgmentStore : IAtOwnRiskAcknowledgmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, AtOwnRiskAcknowledgment> _items = new(StringComparer.OrdinalIgnoreCase);

    public JsonAtOwnRiskAcknowledgmentStore(string? path = null)
    {
        _path = path ?? NlPartnershipPaths.Acknowledgments;
        Load();
    }

    public AtOwnRiskAcknowledgment? Get(string playerId, string gameId)
    {
        lock (_lock)
        {
            return _items.TryGetValue(Key(playerId, gameId), out var a) ? a : null;
        }
    }

    public void Save(AtOwnRiskAcknowledgment acknowledgment)
    {
        lock (_lock)
        {
            _items[Key(acknowledgment.PlayerId, acknowledgment.GameId)] = acknowledgment;
            Persist();
        }
    }

    public IReadOnlyList<AtOwnRiskAcknowledgment> ListForPlayer(string playerId)
    {
        lock (_lock)
        {
            return _items.Values
                .Where(a => string.Equals(a.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private static string Key(string playerId, string gameId) =>
        $"{playerId.Trim().ToLowerInvariant()}::{gameId.Trim().ToLowerInvariant()}";

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var json = File.ReadAllText(_path);
        var list = JsonSerializer.Deserialize<List<AtOwnRiskAcknowledgment>>(json, JsonOptions);
        if (list is null)
        {
            return;
        }

        _items = list.ToDictionary(a => Key(a.PlayerId, a.GameId), StringComparer.OrdinalIgnoreCase);
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_items.Values.ToList(), JsonOptions));
    }
}
