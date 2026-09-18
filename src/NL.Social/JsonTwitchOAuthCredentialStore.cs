using System.Text.Json;
using NL.Social.Core;

namespace NL.Social;

public sealed class JsonTwitchOAuthCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, TwitchOAuthCredential> _byPlayer = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _playerByTwitchUser = new(StringComparer.Ordinal);

    public JsonTwitchOAuthCredentialStore(string? path = null)
    {
        _path = path ?? NlSocialPaths.TwitchCredentials;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public TwitchOAuthCredential? GetByPlayer(string playerId)
    {
        lock (_lock)
        {
            return _byPlayer.GetValueOrDefault(playerId.Trim());
        }
    }

    public string? GetPlayerForTwitchUser(string twitchUserId)
    {
        lock (_lock)
        {
            return _playerByTwitchUser.GetValueOrDefault(twitchUserId.Trim());
        }
    }

    public TwitchOAuthCredential Save(TwitchOAuthCredential credential)
    {
        var playerId = credential.PlayerId.Trim();
        var twitchUserId = credential.TwitchUserId.Trim();

        lock (_lock)
        {
            if (_playerByTwitchUser.TryGetValue(twitchUserId, out var existingPlayer)
                && !string.Equals(existingPlayer, playerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new TwitchLinkConflictException(twitchUserId, existingPlayer);
            }

            if (_byPlayer.TryGetValue(playerId, out var previous)
                && !string.Equals(previous.TwitchUserId, twitchUserId, StringComparison.Ordinal))
            {
                _playerByTwitchUser.Remove(previous.TwitchUserId);
            }

            _byPlayer[playerId] = credential with { PlayerId = playerId, TwitchUserId = twitchUserId };
            _playerByTwitchUser[twitchUserId] = playerId;
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
            var list = JsonSerializer.Deserialize<List<TwitchOAuthCredential>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _byPlayer = list.ToDictionary(c => c.PlayerId, StringComparer.OrdinalIgnoreCase);
            _playerByTwitchUser = list.ToDictionary(c => c.TwitchUserId, c => c.PlayerId, StringComparer.Ordinal);
        }
        catch
        {
            _byPlayer = new Dictionary<string, TwitchOAuthCredential>(StringComparer.OrdinalIgnoreCase);
            _playerByTwitchUser = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
