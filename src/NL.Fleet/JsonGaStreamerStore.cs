using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonGaStreamerStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonGaStreamerStore(string? path = null) =>
        _path = path ?? NlFleetPaths.GaStreamers;

    public IReadOnlyList<GaStreamerEntry> List()
    {
        lock (_lock)
        {
            return LoadUnsafe();
        }
    }

    public GaStreamerEntry? GetByStreamerId(string streamerId)
    {
        lock (_lock)
        {
            return LoadUnsafe().FirstOrDefault(e =>
                string.Equals(e.StreamerId, streamerId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public GaStreamerEntry Save(GaStreamerEntry entry)
    {
        lock (_lock)
        {
            var list = LoadUnsafe().ToList();
            var idx = list.FindIndex(e => string.Equals(e.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                list[idx] = entry;
            }
            else
            {
                list.Add(entry);
            }

            WriteUnsafe(list);
            return entry;
        }
    }

    private List<GaStreamerEntry> LoadUnsafe()
    {
        NlFleetPaths.EnsureRoot();
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<GaStreamerEntry>>(File.ReadAllText(_path), JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void WriteUnsafe(List<GaStreamerEntry> list) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(list, JsonOptions));
}
