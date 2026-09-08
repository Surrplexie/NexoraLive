using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonBetaWaitlistStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonBetaWaitlistStore(string? path = null) =>
        _path = path ?? NlFleetPaths.BetaWaitlist;

    public IReadOnlyList<BetaWaitlistEntry> List()
    {
        lock (_lock)
        {
            return LoadUnsafe();
        }
    }

    public BetaWaitlistEntry? Get(string id)
    {
        lock (_lock)
        {
            return LoadUnsafe().FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    public BetaWaitlistEntry Save(BetaWaitlistEntry entry)
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

    public bool IsApprovedStreamer(string streamerId)
    {
        lock (_lock)
        {
            return LoadUnsafe().Any(e =>
                e.Status == BetaWaitlistStatus.Approved
                && string.Equals(e.ApprovedStreamerId, streamerId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private List<BetaWaitlistEntry> LoadUnsafe()
    {
        NlFleetPaths.EnsureRoot();
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<BetaWaitlistEntry>>(File.ReadAllText(_path), JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void WriteUnsafe(List<BetaWaitlistEntry> list) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(list, JsonOptions));
}
