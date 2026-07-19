using System.Text.Json;
using System.Text.Json.Serialization;
using NL.Core.Sp;
using NL.Moderation.Core;

namespace NL.Moderation;

/// <summary>
/// Durable <see cref="ISpProfileRepository"/> backed by a single JSON file (loaded once on
/// construction, rewritten whole on every mutation — fine at prototype scale). Default
/// location: <c>%LOCALAPPDATA%\NL\sp-profiles.json</c>.
/// </summary>
public sealed class JsonFileSpProfileRepository : ISpProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly Dictionary<string, SpProfile> _profiles = new();
    private readonly object _lock = new();

    public JsonFileSpProfileRepository(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public SpProfile? Find(string playerId)
    {
        lock (_lock)
        {
            return _profiles.TryGetValue(playerId, out var profile) ? profile : null;
        }
    }

    public SpProfile GetOrCreate(string playerId, string displayName)
    {
        lock (_lock)
        {
            if (_profiles.TryGetValue(playerId, out var existing))
            {
                return existing;
            }

            var created = new SpProfile
            {
                Id = playerId,
                DisplayName = displayName,
                AccountCreatedAtUtc = DateTimeOffset.UtcNow,
            };
            _profiles[playerId] = created;
            SaveInternal();
            return created;
        }
    }

    public void Save(SpProfile profile)
    {
        lock (_lock)
        {
            _profiles[profile.Id] = profile;
            SaveInternal();
        }
    }

    public IReadOnlyList<SpProfile> All()
    {
        lock (_lock)
        {
            return _profiles.Values.ToList();
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

        var dtos = JsonSerializer.Deserialize<List<SpProfileDto>>(json, JsonOptions) ?? new();
        foreach (var dto in dtos)
        {
            _profiles[dto.Id] = dto.ToProfile();
        }
    }

    private void SaveInternal()
    {
        var dtos = _profiles.Values.Select(SpProfileDto.FromProfile).ToList();
        var json = JsonSerializer.Serialize(dtos, JsonOptions);
        File.WriteAllText(_path, json);
    }
}
