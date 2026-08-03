using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonFleetValidationStore : IFleetValidationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonFleetValidationStore(string? path = null) => _path = path ?? NlFleetPaths.ValidationReport;

    public FleetValidationReport? GetLast()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        lock (_lock)
        {
            return JsonSerializer.Deserialize<FleetValidationReport>(File.ReadAllText(_path), JsonOptions);
        }
    }

    public void Save(FleetValidationReport report)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        lock (_lock)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(report, JsonOptions));
        }
    }
}
