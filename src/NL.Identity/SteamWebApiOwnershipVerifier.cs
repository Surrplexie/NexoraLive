using System.Net.Http;
using System.Text.Json;
using NL.Identity.Core;

namespace NL.Identity;

/// <summary>Steam Web API ownership + ban checks when <c>STEAM_WEB_API_KEY</c> is set.</summary>
public sealed class SteamWebApiOwnershipVerifier : IGameOwnershipVerifier, IPublisherBanChecker
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly MockGameOwnershipVerifier _fallback;

    public SteamWebApiOwnershipVerifier(HttpClient? http = null, string? apiKey = null, MockGameOwnershipVerifier? fallback = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("STEAM_WEB_API_KEY") ?? "";
        _fallback = fallback ?? new MockGameOwnershipVerifier();
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != NlPlatform.Steam)
        {
            return await _fallback.VerifyAsync(request, cancellationToken);
        }

        if (!IsConfigured)
        {
            return new GameOwnershipResult(
                GameOwnershipStatus.Unknown,
                "STEAM_WEB_API_KEY not configured; set key or use mock ownership file.");
        }

        var appId = request.AppId ?? request.GameId;
        if (string.IsNullOrWhiteSpace(appId))
        {
            return new GameOwnershipResult(GameOwnershipStatus.NotOwned, "Steam app id required.");
        }

        if (await IsPublisherBannedAsync(request.Platform, request.PlatformUserId, request.GameId, appId, cancellationToken))
        {
            return new GameOwnershipResult(GameOwnershipStatus.Banned, "Steam VAC/game ban active.", PublisherBanned: true);
        }

        var url =
            $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(_apiKey)}&steamid={Uri.EscapeDataString(request.PlatformUserId)}&include_appinfo=0&include_played_free_games=1&appids_filter[0]={Uri.EscapeDataString(appId)}";
        using var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, $"Steam API HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var count = doc.RootElement
            .GetProperty("response")
            .GetProperty("game_count")
            .GetInt32();

        return count > 0
            ? new GameOwnershipResult(GameOwnershipStatus.Owned)
            : new GameOwnershipResult(GameOwnershipStatus.NotOwned, $"Steam app {appId} not in library.");
    }

    public async Task<bool> IsPublisherBannedAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        string? appId = null,
        CancellationToken cancellationToken = default)
    {
        if (platform != NlPlatform.Steam || !IsConfigured)
        {
            return await _fallback.IsPublisherBannedAsync(platform, platformUserId, gameId, appId, cancellationToken);
        }

        var url =
            $"https://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key={Uri.EscapeDataString(_apiKey)}&steamids={Uri.EscapeDataString(platformUserId)}";
        using var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!doc.RootElement.TryGetProperty("players", out var players) || players.GetArrayLength() == 0)
        {
            return false;
        }

        var player = players[0];
        return player.TryGetProperty("VACBanned", out var vac) && vac.GetBoolean()
            || player.TryGetProperty("NumberOfGameBans", out var gameBans) && gameBans.GetInt32() > 0;
    }
}
