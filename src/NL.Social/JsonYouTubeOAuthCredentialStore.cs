using System.Text.Json;
using NL.Social.Core;

namespace NL.Social;

public sealed class JsonYouTubeOAuthCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, YouTubeOAuthCredential> _byPlayer = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _playerByYouTubeChannel = new(StringComparer.Ordinal);

    public JsonYouTubeOAuthCredentialStore(string? path = null)
    {
        _path = path ?? NlSocialPaths.YouTubeCredentials;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public YouTubeOAuthCredential? GetByPlayer(string playerId)
    {
        lock (_lock)
        {
            return _byPlayer.GetValueOrDefault(playerId.Trim());
        }
    }

    public YouTubeOAuthCredential Save(YouTubeOAuthCredential credential)
    {
        var playerId = credential.PlayerId.Trim();
        var channelId = credential.YouTubeChannelId.Trim();

        lock (_lock)
        {
            if (_playerByYouTubeChannel.TryGetValue(channelId, out var existingPlayer)
                && !string.Equals(existingPlayer, playerId, StringComparison.OrdinalIgnoreCase))
            {
                throw new YouTubeLinkConflictException(channelId, existingPlayer);
            }

            if (_byPlayer.TryGetValue(playerId, out var previous)
                && !string.Equals(previous.YouTubeChannelId, channelId, StringComparison.Ordinal))
            {
                _playerByYouTubeChannel.Remove(previous.YouTubeChannelId);
            }

            _byPlayer[playerId] = credential with { PlayerId = playerId, YouTubeChannelId = channelId };
            _playerByYouTubeChannel[channelId] = playerId;
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
            var list = JsonSerializer.Deserialize<List<YouTubeOAuthCredential>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _byPlayer = list.ToDictionary(c => c.PlayerId, StringComparer.OrdinalIgnoreCase);
            _playerByYouTubeChannel = list.ToDictionary(c => c.YouTubeChannelId, c => c.PlayerId, StringComparer.Ordinal);
        }
        catch
        {
            _byPlayer = new Dictionary<string, YouTubeOAuthCredential>(StringComparer.OrdinalIgnoreCase);
            _playerByYouTubeChannel = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
