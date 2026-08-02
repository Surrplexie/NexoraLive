using System.Text.Json;
using System.Text.Json.Serialization;
using NL.Social.Core;

namespace NL.Social;

public sealed class JsonStreamerSocialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, StreamerSocialConfig> _configs = new();

    public JsonStreamerSocialStore(string? path = null)
    {
        _path = path ?? NlSocialPaths.StreamerConfig;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public StreamerSocialConfig GetOrDefault(string streamerId)
    {
        lock (_lock)
        {
            return _configs.TryGetValue(streamerId, out var config)
                ? config
                : StreamerSocialConfig.Empty(streamerId);
        }
    }

    public void Save(StreamerSocialConfig config)
    {
        lock (_lock)
        {
            _configs[config.StreamerId] = config;
            File.WriteAllText(_path, JsonSerializer.Serialize(_configs.Values.ToList(), JsonOptions));
        }
    }

    public IReadOnlyList<StreamerSocialConfig> All()
    {
        lock (_lock)
        {
            return _configs.Values.ToList();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var json = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var list = JsonSerializer.Deserialize<List<StreamerSocialConfig>>(json, JsonOptions);
        if (list is null)
        {
            return;
        }

        _configs = list.ToDictionary(c => c.StreamerId, StringComparer.OrdinalIgnoreCase);
    }
}
