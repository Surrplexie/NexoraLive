using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonProductionDogfoodRunStore
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonProductionDogfoodRunStore(string? path = null) =>
        _path = path ?? NlFleetPaths.ProductionDogfoodLastRun;

    public ProductionDogfoodLastRun? GetLast()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<ProductionDogfoodLastRun>(
                    File.ReadAllText(_path),
                    JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }

    public void Save(ProductionDogfoodLastRun run)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, System.Text.Json.JsonSerializer.Serialize(run, JsonOptions));
        }
    }
}
