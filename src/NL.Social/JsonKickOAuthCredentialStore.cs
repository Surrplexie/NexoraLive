using System.Text.Json;
using NL.Social.Core;

namespace NL.Social;

public sealed class JsonKickOAuthCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, KickOAuthCredential> _byPlayer = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _playerByKickUser = new(StringComparer.Ordinal);

    public JsonKickOAuthCredentialStore(string? path = null)
    {
        _path = path ?? NlSocialPaths.KickCredentials;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public KickOAuthCredential? GetByPlayer(string playerId)
    {
        lock (_lock)
        {
            return _byPlayer.GetValueOrDefault(playerId.Trim());
        }
    }

    public KickOAuthCredential Save(KickOAuthCredential credential)
    {
        var playerId = credential.PlayerId.Trim();
        var kickUserId = credential.KickUserId.Trim();

        lock (_lock)
        {
            if (_playerByKickUser.TryGetValue(kickUserId, out var existingPlayer)
                && !string.Equals(existingPlayer, playerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new KickLinkConflictException(kickUserId, existingPlayer);
            }

            if (_byPlayer.TryGetValue(playerId, out var previous)
                && !string.Equals(previous.KickUserId, kickUserId, StringComparison.Ordinal))
            {
                _playerByKickUser.Remove(previous.KickUserId);
            }

            _byPlayer[playerId] = credential with { PlayerId = playerId, KickUserId = kickUserId };
            _playerByKickUser[kickUserId] = playerId;
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
            var list = JsonSerializer.Deserialize<List<KickOAuthCredential>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _byPlayer = list.ToDictionary(c => c.PlayerId, StringComparer.OrdinalIgnoreCase);
            _playerByKickUser = list.ToDictionary(c => c.KickUserId, c => c.PlayerId, StringComparer.Ordinal);
        }
        catch
        {
            _byPlayer = new Dictionary<string, KickOAuthCredential>(StringComparer.OrdinalIgnoreCase);
            _playerByKickUser = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
