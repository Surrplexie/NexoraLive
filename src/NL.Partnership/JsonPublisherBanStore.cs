using System.Text.Json;
using NL.Partnership.Core;

namespace NL.Partnership;

public sealed class JsonPublisherBanStore : IPublisherBanStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private List<PublisherBanEntry> _entries = [];

    public JsonPublisherBanStore(string? path = null)
    {
        _path = path ?? NlPartnershipPaths.Bans;
        Load();
    }

    public IReadOnlyList<PublisherBanEntry> ListForGame(string gameId)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => string.Equals(e.GameId, gameId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public bool IsBanned(string gameId, string platformUserId, DateTimeOffset nowUtc)
    {
        lock (_lock)
        {
            return _entries.Any(e =>
                string.Equals(e.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase)
                && (e.ExpiresAtUtc is null || e.ExpiresAtUtc > nowUtc));
        }
    }

    public void Ban(PublisherBanEntry entry)
    {
        lock (_lock)
        {
            _entries.RemoveAll(e =>
                string.Equals(e.GameId, entry.GameId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.PlatformUserId, entry.PlatformUserId, StringComparison.OrdinalIgnoreCase));
            _entries.Add(entry);
            Persist();
        }
    }

    public void Unban(string gameId, string platformUserId)
    {
        lock (_lock)
        {
            _entries.RemoveAll(e =>
                string.Equals(e.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.PlatformUserId, platformUserId, StringComparison.OrdinalIgnoreCase));
            Persist();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var json = File.ReadAllText(_path);
        _entries = JsonSerializer.Deserialize<List<PublisherBanEntry>>(json, JsonOptions) ?? [];
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_entries, JsonOptions));
    }
}
