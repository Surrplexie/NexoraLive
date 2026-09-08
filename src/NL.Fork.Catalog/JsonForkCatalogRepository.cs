using System.Text.Json;
using System.Text.Json.Serialization;
using NL.Fork.Catalog.Core;

namespace NL.Fork.Catalog;

public sealed class JsonForkCatalogRepository : IForkCatalogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonForkCatalogRepository(string? path = null)
    {
        _path = path ?? NlForkCatalogPaths.Manifest;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public ForkCatalogManifest Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                return new ForkCatalogManifest();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<ForkCatalogManifest>(json, JsonOptions)
                   ?? new ForkCatalogManifest();
        }
    }

    public void Save(ForkCatalogManifest manifest)
    {
        lock (_lock)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(manifest, JsonOptions));
        }
    }
}
