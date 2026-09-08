using System.Text.Json;
using NL.Identity.Core;

namespace NL.Identity;

/// <summary>Dev/test ownership matrix from <see cref="NlIdentityPaths.MockOwnershipConfig"/>.</summary>
public sealed class MockGameOwnershipVerifier : IGameOwnershipVerifier, IPublisherBanChecker, IMultiplayerSubscriptionChecker
{
    private readonly object _lock = new();
    private MockOwnershipDatabase _db;

    public MockGameOwnershipVerifier(string? configPath = null)
    {
        _db = Load(configPath ?? NlIdentityPaths.MockOwnershipConfig);
    }

    public void Reload() => _db = Load(NlIdentityPaths.MockOwnershipConfig);

    public Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var key = NlPlatformNames.LinkKey(request.Platform, request.PlatformUserId);
        var appKey = request.AppId ?? request.GameId;

        lock (_lock)
        {
            if (_db.Banned.TryGetValue(key, out var banned) && banned)
            {
                return Task.FromResult(new GameOwnershipResult(
                    GameOwnershipStatus.Banned,
                    "Publisher ban (mock)",
                    PublisherBanned: true));
            }

            if (_db.SubscriptionRequired.TryGetValue(key, out var subRequired) && subRequired
                && !_db.MultiplayerActive.GetValueOrDefault(key, true))
            {
                return Task.FromResult(new GameOwnershipResult(
                    GameOwnershipStatus.SubscriptionRequired,
                    "Multiplayer subscription required (mock)",
                    MultiplayerSubscriptionActive: false));
            }

            if (_db.Ownership.TryGetValue(key, out var games)
                && games.TryGetValue(appKey, out var statusText))
            {
                var status = ParseStatus(statusText);
                return Task.FromResult(new GameOwnershipResult(
                    status,
                    status == GameOwnershipStatus.Owned ? null : $"Mock: not owner of app {appKey}"));
            }

            if (_db.DefaultOwned)
            {
                return Task.FromResult(new GameOwnershipResult(GameOwnershipStatus.Owned));
            }

            return Task.FromResult(new GameOwnershipResult(
                GameOwnershipStatus.NotOwned,
                $"Mock: no ownership record for {appKey}"));
        }
    }

    public Task<bool> IsPublisherBannedAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        string? appId = null,
        CancellationToken cancellationToken = default)
    {
        var key = NlPlatformNames.LinkKey(platform, platformUserId);
        lock (_lock)
        {
            return Task.FromResult(_db.Banned.GetValueOrDefault(key, false));
        }
    }

    public Task<bool> HasMultiplayerAccessAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        var key = NlPlatformNames.LinkKey(platform, platformUserId);
        lock (_lock)
        {
            if (_db.SubscriptionRequired.GetValueOrDefault(key, false))
            {
                return Task.FromResult(_db.MultiplayerActive.GetValueOrDefault(key, false));
            }

            return Task.FromResult(true);
        }
    }

    private static GameOwnershipStatus ParseStatus(string text) =>
        Enum.TryParse<GameOwnershipStatus>(text, ignoreCase: true, out var status)
            ? status
            : GameOwnershipStatus.Unknown;

    private static MockOwnershipDatabase Load(string path)
    {
        if (!File.Exists(path))
        {
            return new MockOwnershipDatabase { DefaultOwned = false };
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MockOwnershipDatabase>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? new MockOwnershipDatabase();
    }

    private sealed class MockOwnershipDatabase
    {
        public bool DefaultOwned { get; set; }

        public Dictionary<string, Dictionary<string, string>> Ownership { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, bool> Banned { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, bool> SubscriptionRequired { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, bool> MultiplayerActive { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
