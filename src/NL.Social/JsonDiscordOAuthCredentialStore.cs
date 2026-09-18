using System.Text.Json;
using NL.Social.Core;

namespace NL.Social;

public sealed class JsonDiscordOAuthCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, DiscordOAuthCredential> _byPlayer = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _playerByDiscordUser = new(StringComparer.Ordinal);

    public JsonDiscordOAuthCredentialStore(string? path = null)
    {
        _path = path ?? NlSocialPaths.DiscordCredentials;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public DiscordOAuthCredential? GetByPlayer(string playerId)
    {
        lock (_lock)
        {
            return _byPlayer.GetValueOrDefault(playerId.Trim());
        }
    }

    public string? GetPlayerForDiscordUser(string discordUserId)
    {
        lock (_lock)
        {
            return _playerByDiscordUser.GetValueOrDefault(discordUserId.Trim());
        }
    }

    public DiscordOAuthCredential Save(DiscordOAuthCredential credential)
    {
        var playerId = credential.PlayerId.Trim();
        var discordUserId = credential.DiscordUserId.Trim();

        lock (_lock)
        {
            if (_playerByDiscordUser.TryGetValue(discordUserId, out var existingPlayer)
                && !string.Equals(existingPlayer, playerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new DiscordLinkConflictException(discordUserId, existingPlayer);
            }

            if (_byPlayer.TryGetValue(playerId, out var previous)
                && !string.Equals(previous.DiscordUserId, discordUserId, StringComparison.Ordinal))
            {
                _playerByDiscordUser.Remove(previous.DiscordUserId);
            }

            _byPlayer[playerId] = credential with { PlayerId = playerId, DiscordUserId = discordUserId };
            _playerByDiscordUser[discordUserId] = playerId;
            File.WriteAllText(_path, JsonSerializer.Serialize(_byPlayer.Values.ToList(), JsonOptions));
            return _byPlayer[playerId];
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<DiscordOAuthCredential>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _byPlayer = list.ToDictionary(c => c.PlayerId, StringComparer.OrdinalIgnoreCase);
            _playerByDiscordUser = list.ToDictionary(c => c.DiscordUserId, c => c.PlayerId, StringComparer.Ordinal);
        }
        catch
        {
            _byPlayer = new Dictionary<string, DiscordOAuthCredential>(StringComparer.OrdinalIgnoreCase);
            _playerByDiscordUser = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
