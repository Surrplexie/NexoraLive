using System.Text.Json;
using NL.Fork.Catalog.Core;
using NL.Partnership.Core;

namespace NL.Partnership;

public sealed class JsonPlatformOptInStore : IPlatformOptInStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private List<PlatformOptInEntry> _entries = [];

    public JsonPlatformOptInStore(string? path = null)
    {
        _path = path ?? NlPartnershipPaths.PlatformOptIn;
        Load();
    }

    public IReadOnlyList<PlatformOptInEntry> List(bool enabledOnly = true)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => !enabledOnly || e.Enabled)
                .OrderBy(e => e.Platform)
                .ThenBy(e => e.AppId)
                .ToList();
        }
    }

    public PlatformOptInEntry? Find(string platform, string appId)
    {
        lock (_lock)
        {
            return _entries.FirstOrDefault(e =>
                string.Equals(e.Platform, platform, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.AppId, appId, StringComparison.OrdinalIgnoreCase)
                && e.Enabled);
        }
    }

    public void Save(PlatformOptInEntry entry)
    {
        lock (_lock)
        {
            var idx = _entries.FindIndex(e =>
                string.Equals(e.Platform, entry.Platform, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.AppId, entry.AppId, StringComparison.OrdinalIgnoreCase));
            var saved = entry with { UpdatedAtUtc = DateTimeOffset.UtcNow };
            if (idx >= 0)
            {
                _entries[idx] = saved;
            }
            else
            {
                _entries.Add(saved);
            }

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
        _entries = JsonSerializer.Deserialize<List<PlatformOptInEntry>>(json, JsonOptions) ?? [];
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
