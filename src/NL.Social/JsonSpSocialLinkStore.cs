using System.Text.Json;
using NL.Social.Core;

namespace NL.Social;

public sealed class JsonSpSocialLinkStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, SpSocialLinks> _links = new();

    public JsonSpSocialLinkStore(string? path = null)
    {
        _path = path ?? NlSocialPaths.SpLinks;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public SpSocialLinks GetOrDefault(string playerId)
    {
        lock (_lock)
        {
            return _links.TryGetValue(playerId, out var links)
                ? links
                : new SpSocialLinks(playerId);
        }
    }

    public SpSocialLinks Save(SpSocialLinks links)
    {
        lock (_lock)
        {
            var merged = Merge(_links.GetValueOrDefault(links.PlayerId), links);
            _links[links.PlayerId] = merged;
            File.WriteAllText(_path, JsonSerializer.Serialize(_links.Values.ToList(), JsonOptions));
            return merged;
        }
    }

    private static SpSocialLinks Merge(SpSocialLinks? existing, SpSocialLinks incoming) => new(
        incoming.PlayerId,
        incoming.TwitchUserId ?? existing?.TwitchUserId,
        incoming.YouTubeChannelId ?? existing?.YouTubeChannelId,
        incoming.KickUserId ?? existing?.KickUserId,
        incoming.DiscordUserId ?? existing?.DiscordUserId);

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

        var list = JsonSerializer.Deserialize<List<SpSocialLinks>>(json, JsonOptions);
        if (list is null)
        {
            return;
        }

        _links = list.ToDictionary(l => l.PlayerId, StringComparer.OrdinalIgnoreCase);
    }
}
