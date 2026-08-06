using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NL.Identity.Core;

namespace NL.Identity;

/// <summary>Refresh per-platform OAuth tokens stored for linked accounts.</summary>
public sealed class PlatformOAuthTokenService
{
    private readonly JsonPlatformOAuthCredentialStore _credentials;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public PlatformOAuthTokenService(
        JsonPlatformOAuthCredentialStore credentials,
        NlTokenProtector? tokenProtector = null,
        HttpClient? http = null)
    {
        _credentials = credentials;
        _tokenProtector = tokenProtector ?? new NlTokenProtector();
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<string?> GetValidAccessTokenAsync(
        NlPlatform platform,
        string externalUserId,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentials.GetByPlatformUser(platform, externalUserId);
        if (credential is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(credential.ProtectedAccessToken)
            && credential.AccessTokenExpiresUtc is { } expires
            && expires > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _tokenProtector.Unprotect(credential.ProtectedAccessToken);
        }

        var refreshToken = _tokenProtector.Unprotect(credential.ProtectedRefreshToken);
        var refreshed = platform switch
        {
            NlPlatform.Epic => await RefreshEpicAsync(refreshToken, cancellationToken),
            NlPlatform.Xbox => await RefreshXboxAsync(refreshToken, cancellationToken),
            NlPlatform.PlayStation => await RefreshPlayStationAsync(refreshToken, cancellationToken),
            _ => (null, null, 0),
        };

        if (refreshed.AccessToken is null)
        {
            return null;
        }

        var updated = credential with
        {
            ProtectedAccessToken = _tokenProtector.Protect(refreshed.AccessToken),
            AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, refreshed.ExpiresIn - 60)),
            ProtectedRefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                ? credential.ProtectedRefreshToken
                : _tokenProtector.Protect(refreshed.RefreshToken!),
        };
        _credentials.Save(updated);
        return refreshed.AccessToken;
    }

    private async Task<(string? AccessToken, string? RefreshToken, int ExpiresIn)> RefreshEpicAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (!PlatformOAuthEnv.HasPair("EPIC_CLIENT_ID", "EPIC_CLIENT_SECRET"))
        {
            return (null, null, 0);
        }

        return await PlatformOAuthHttp.ExchangeCodePublicAsync(
            _http,
            "https://api.epicgames.dev/epic/oauth/v2/token",
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = PlatformOAuthEnv.Get("EPIC_CLIENT_ID")!.Trim(),
                ["client_secret"] = PlatformOAuthEnv.Get("EPIC_CLIENT_SECRET")!.Trim(),
            },
            cancellationToken);
    }

    private async Task<(string? AccessToken, string? RefreshToken, int ExpiresIn)> RefreshXboxAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var clientId = PlatformOAuthEnv.Get("XBOX_CLIENT_ID") ?? PlatformOAuthEnv.Get("MICROSOFT_CLIENT_ID");
        var clientSecret = PlatformOAuthEnv.Get("XBOX_CLIENT_SECRET") ?? PlatformOAuthEnv.Get("MICROSOFT_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return (null, null, 0);
        }

        return await PlatformOAuthHttp.ExchangeCodePublicAsync(
            _http,
            "https://login.live.com/oauth20_token.srf",
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId.Trim(),
                ["client_secret"] = clientSecret.Trim(),
            },
            cancellationToken);
    }

    private async Task<(string? AccessToken, string? RefreshToken, int ExpiresIn)> RefreshPlayStationAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (!PlatformOAuthEnv.HasPair("PSN_CLIENT_ID", "PSN_CLIENT_SECRET"))
        {
            return (null, null, 0);
        }

        return await PlatformOAuthHttp.ExchangeCodePublicAsync(
            _http,
            "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/token",
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = PlatformOAuthEnv.Get("PSN_CLIENT_ID")!.Trim(),
                ["client_secret"] = PlatformOAuthEnv.Get("PSN_CLIENT_SECRET")!.Trim(),
            },
            cancellationToken);
    }
}

/// <summary>Epic Ecom ownership API (Phase L.3).</summary>
public sealed class EpicOwnershipVerifier : IGameOwnershipVerifier
{
    private readonly MockGameOwnershipVerifier _fallback;
    private readonly HttpClient _http;

    public EpicOwnershipVerifier(MockGameOwnershipVerifier fallback, HttpClient? http = null)
    {
        _fallback = fallback;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public bool IsConfigured => PlatformOAuthEnv.HasPair("EPIC_CLIENT_ID", "EPIC_CLIENT_SECRET");

    public async Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != NlPlatform.Epic)
        {
            return await _fallback.VerifyAsync(request, cancellationToken);
        }

        if (!IsConfigured)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, "EPIC_CLIENT_ID/SECRET not configured.");
        }

        var catalogItemId = request.AppId ?? request.GameId;
        if (string.IsNullOrWhiteSpace(catalogItemId))
        {
            return new GameOwnershipResult(GameOwnershipStatus.NotOwned, "Epic catalog item id required.");
        }

        var clientToken = await PlatformOAuthHttp.ClientCredentialsTokenAsync(
            _http,
            "https://api.epicgames.dev/epic/oauth/v2/token",
            PlatformOAuthEnv.Get("EPIC_CLIENT_ID")!.Trim(),
            PlatformOAuthEnv.Get("EPIC_CLIENT_SECRET")!.Trim(),
            cancellationToken);

        if (clientToken is null)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, "Epic client credentials token failed.");
        }

        using var ownershipRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.epicgames.dev/epic/ecom/v1/ownership")
        {
            Content = JsonContent.Create(new
            {
                identityId = request.PlatformUserId,
                nsCatalogItemId = catalogItemId,
            }),
        };
        ownershipRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);

        using var response = await _http.SendAsync(ownershipRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, $"Epic ownership API HTTP {(int)response.StatusCode}");
        }

        var body = await response.Content.ReadFromJsonAsync<EpicOwnershipResponse>(cancellationToken);
        var owned = body?.ItemOwnership?.Any(i => i.Owned) == true;
        return owned
            ? new GameOwnershipResult(GameOwnershipStatus.Owned, "epic-ecom")
            : new GameOwnershipResult(GameOwnershipStatus.NotOwned, $"Epic catalog item {catalogItemId} not owned.");
    }

    private sealed class EpicOwnershipResponse
    {
        [JsonPropertyName("itemOwnership")]
        public List<EpicOwnedItem>? ItemOwnership { get; set; }
    }

    private sealed class EpicOwnedItem
    {
        [JsonPropertyName("owned")]
        public bool Owned { get; set; }
    }
}

/// <summary>Xbox Title Hub ownership API (Phase L.3).</summary>
public sealed class XboxOwnershipVerifier : IGameOwnershipVerifier, IMultiplayerSubscriptionChecker
{
    private readonly MockGameOwnershipVerifier _fallback;
    private readonly PlatformOAuthTokenService _tokens;
    private readonly HttpClient _http;

    public XboxOwnershipVerifier(
        MockGameOwnershipVerifier fallback,
        PlatformOAuthTokenService tokens,
        HttpClient? http = null)
    {
        _fallback = fallback;
        _tokens = tokens;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public bool IsConfigured => PlatformOAuthEnv.HasPair("XBOX_CLIENT_ID", "XBOX_CLIENT_SECRET")
        || PlatformOAuthEnv.HasPair("MICROSOFT_CLIENT_ID", "MICROSOFT_CLIENT_SECRET");

    public async Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != NlPlatform.Xbox)
        {
            return await _fallback.VerifyAsync(request, cancellationToken);
        }

        if (!IsConfigured)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, "XBOX_CLIENT_ID/SECRET not configured.");
        }

        var titleId = request.AppId ?? request.GameId;
        if (string.IsNullOrWhiteSpace(titleId))
        {
            return new GameOwnershipResult(GameOwnershipStatus.NotOwned, "Xbox title id required.");
        }

        var accessToken = await _tokens.GetValidAccessTokenAsync(NlPlatform.Xbox, request.PlatformUserId, cancellationToken);
        if (accessToken is null)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, "No Xbox OAuth token for this XUID.");
        }

        var xsts = await XboxLiveAuthHelper.BuildTitleHubTokensAsync(_http, accessToken, cancellationToken);
        if (xsts is null)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, "Xbox Live auth failed.");
        }

        var url =
            $"https://titlehub.xboxlive.com/users/xuid({Uri.EscapeDataString(request.PlatformUserId)})/titles/batch/decoration/GamePass,ProductId?titleIds={Uri.EscapeDataString(titleId)}";
        using var titleRequest = new HttpRequestMessage(HttpMethod.Get, url);
        titleRequest.Headers.Add("x-xbl-contract-version", "2");
        titleRequest.Headers.Authorization = new AuthenticationHeaderValue("XBL3.0", $"x={xsts.UserHash};{xsts.XstsToken}");

        using var response = await _http.SendAsync(titleRequest, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new GameOwnershipResult(GameOwnershipStatus.NotOwned, $"Xbox title {titleId} not in library.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, $"Xbox Title Hub HTTP {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var hasTitle = doc.RootElement.TryGetProperty("titles", out var titles) && titles.GetArrayLength() > 0;
        return hasTitle
            ? new GameOwnershipResult(GameOwnershipStatus.Owned, "xbox-titlehub")
            : new GameOwnershipResult(GameOwnershipStatus.NotOwned, $"Xbox title {titleId} not in library.");
    }

    public async Task<bool> HasMultiplayerAccessAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        if (platform != NlPlatform.Xbox)
        {
            return await _fallback.HasMultiplayerAccessAsync(platform, platformUserId, gameId, cancellationToken);
        }

        var result = await VerifyAsync(new GameOwnershipRequest(platform, platformUserId, gameId, gameId), cancellationToken);
        return result.Status is GameOwnershipStatus.Owned or GameOwnershipStatus.Unknown;
    }
}

/// <summary>PlayStation entitlements API (Phase L.3).</summary>
public sealed class PlayStationOwnershipVerifier : IGameOwnershipVerifier, IMultiplayerSubscriptionChecker
{
    private readonly MockGameOwnershipVerifier _fallback;
    private readonly PlatformOAuthTokenService _tokens;
    private readonly HttpClient _http;

    public PlayStationOwnershipVerifier(
        MockGameOwnershipVerifier fallback,
        PlatformOAuthTokenService tokens,
        HttpClient? http = null)
    {
        _fallback = fallback;
        _tokens = tokens;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public bool IsConfigured => PlatformOAuthEnv.HasPair("PSN_CLIENT_ID", "PSN_CLIENT_SECRET");

    public async Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != NlPlatform.PlayStation)
        {
            return await _fallback.VerifyAsync(request, cancellationToken);
        }

        if (!IsConfigured)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, "PSN_CLIENT_ID/SECRET not configured.");
        }

        var entitlementId = request.AppId ?? request.GameId;
        if (string.IsNullOrWhiteSpace(entitlementId))
        {
            return new GameOwnershipResult(GameOwnershipStatus.NotOwned, "PlayStation entitlement id required.");
        }

        var accessToken = await _tokens.GetValidAccessTokenAsync(NlPlatform.PlayStation, request.PlatformUserId, cancellationToken);
        if (accessToken is null)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, "No PlayStation OAuth token for this account.");
        }

        var baseUrl = PlatformOAuthEnv.Get("PSN_ENTITLEMENT_BASE_URL")
            ?? "https://m.np.playstation.net/api/entitlement/v2/users/me/entitlements";
        var url = $"{baseUrl.TrimEnd('?')}?entitlementTypes=service,unified&limit=100";

        using var entRequest = new HttpRequestMessage(HttpMethod.Get, url);
        entRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(entRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GameOwnershipResult(GameOwnershipStatus.Unknown, $"PlayStation entitlements HTTP {(int)response.StatusCode}");
        }

        var body = await response.Content.ReadFromJsonAsync<PsnEntitlementsResponse>(cancellationToken);
        var owned = body?.Entitlements?.Any(e =>
            string.Equals(e.Id, entitlementId, StringComparison.OrdinalIgnoreCase)) == true;

        return owned
            ? new GameOwnershipResult(GameOwnershipStatus.Owned, "psn-entitlements")
            : new GameOwnershipResult(GameOwnershipStatus.NotOwned, $"PlayStation entitlement {entitlementId} not found.");
    }

    public async Task<bool> HasMultiplayerAccessAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        if (platform != NlPlatform.PlayStation)
        {
            return await _fallback.HasMultiplayerAccessAsync(platform, platformUserId, gameId, cancellationToken);
        }

        var result = await VerifyAsync(new GameOwnershipRequest(platform, platformUserId, gameId, gameId), cancellationToken);
        if (result.Status == GameOwnershipStatus.SubscriptionRequired)
        {
            return false;
        }

        return result.Status is GameOwnershipStatus.Owned or GameOwnershipStatus.Unknown;
    }

    private sealed class PsnEntitlementsResponse
    {
        [JsonPropertyName("entitlements")]
        public List<PsnEntitlement>? Entitlements { get; set; }
    }

    private sealed class PsnEntitlement
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("activeDate")]
        public string? ActiveDate { get; set; }
    }
}
