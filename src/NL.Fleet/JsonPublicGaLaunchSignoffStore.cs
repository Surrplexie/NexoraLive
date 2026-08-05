using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonPublicGaLaunchSignoffStore
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonPublicGaLaunchSignoffStore(string? path = null) =>
        _path = path ?? NlFleetPaths.PublicGaLaunchSignoff;

    public IReadOnlyList<PublicGaLaunchSignoffEntry> ListRecent(int max = 20)
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<PublicGaLaunchSignoffEntry>>(
                    File.ReadAllText(_path),
                    JsonOptions) ?? [];
                return list.OrderByDescending(e => e.SignedAtUtc).Take(max).ToList();
            }
            catch
            {
                return [];
            }
        }
    }

    public PublicGaLaunchSignoffEntry Record(string operatorId, string launchVersion)
    {
        var entry = new PublicGaLaunchSignoffEntry(
            operatorId,
            launchVersion,
            DateTimeOffset.UtcNow);

        lock (_lock)
        {
            NlFleetPaths.EnsureRoot();
            var list = File.Exists(_path)
                ? System.Text.Json.JsonSerializer.Deserialize<List<PublicGaLaunchSignoffEntry>>(
                    File.ReadAllText(_path),
                    JsonOptions) ?? []
                : [];
            list.Add(entry);
            File.WriteAllText(_path, System.Text.Json.JsonSerializer.Serialize(list, JsonOptions));
        }

        return entry;
    }
}
