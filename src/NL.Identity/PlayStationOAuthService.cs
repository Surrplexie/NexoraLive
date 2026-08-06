using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Identity.Core;

namespace NL.Identity;

/// <summary>PlayStation Network OAuth + account linking (Phase L.3).</summary>
public sealed class PlayStationOAuthService
{
    public const string DefaultScopes = "psn:s2s openid id_token:psn.basic_claims";

    private const string AuthorizeEndpoint = "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/authorize";
    private const string TokenEndpoint = "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/token";

    private readonly JsonOAuthStateStore _stateStore;
    private readonly JsonPlatformOAuthCredentialStore _credentials;
    private readonly NlIdentityService _identity;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public PlayStationOAuthService(
        JsonOAuthStateStore stateStore,
        JsonPlatformOAuthCredentialStore credentials,
        NlIdentityService identity,
        NlTokenProtector? tokenProtector = null,
        HttpClient? http = null)
    {
        _stateStore = stateStore;
        _credentials = credentials;
        _identity = identity;
        _tokenProtector = tokenProtector ?? new NlTokenProtector();
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public bool IsConfigured => PlatformOAuthEnv.HasPair("PSN_CLIENT_ID", "PSN_CLIENT_SECRET");

    public string BuildAuthorizeRedirect(string accountId, string? returnUrl, string publicBaseUrl)
    {
        var clientId = PlatformOAuthEnv.Get("PSN_CLIENT_ID")!.Trim();
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/identity/oauth/playstation/callback";
        var state = _stateStore.Create(accountId, returnUrl, TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = callback,
            ["response_type"] = "code",
            ["scope"] = DefaultScopes,
            ["state"] = state,
        };

        return AuthorizeEndpoint + "?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<PlatformOAuthCallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var pending = ConsumeState(query);
        if (pending.Error is not null)
        {
            return new PlatformOAuthCallbackResult(false, NlPlatform.PlayStation, Error: pending.Error);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return new PlatformOAuthCallbackResult(
                false,
                NlPlatform.PlayStation,
                pending.AccountId,
                ReturnUrl: pending.ReturnUrl,
                Error: "Missing authorization code.");
        }

        var clientId = PlatformOAuthEnv.Get("PSN_CLIENT_ID")!.Trim();
        var clientSecret = PlatformOAuthEnv.Get("PSN_CLIENT_SECRET")!.Trim();
        var redirectUri = $"{publicBaseUrl.TrimEnd('/')}/api/v1/identity/oauth/playstation/callback";

        var (accessToken, refreshToken, expiresIn) = await PlatformOAuthHttp.ExchangeCodePublicAsync(
            _http,
            TokenEndpoint,
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code.Trim(),
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
            },
            cancellationToken);

        if (accessToken is null || refreshToken is null)
        {
            return new PlatformOAuthCallbackResult(
                false,
                NlPlatform.PlayStation,
                pending.AccountId,
                ReturnUrl: pending.ReturnUrl,
                Error: "PlayStation token exchange failed.");
        }

        using var profileRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://m.np.playstation.net/api/profiles/v1/users/me/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var profileResponse = await _http.SendAsync(profileRequest, cancellationToken);
        if (!profileResponse.IsSuccessStatusCode)
        {
            return new PlatformOAuthCallbackResult(
                false,
                NlPlatform.PlayStation,
                pending.AccountId,
                ReturnUrl: pending.ReturnUrl,
                Error: "Failed to load PlayStation profile.");
        }

        var profile = await profileResponse.Content.ReadFromJsonAsync<PsnProfileEnvelope>(cancellationToken);
        var accountId = profile?.Profile?.AccountId;
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return new PlatformOAuthCallbackResult(
                false,
                NlPlatform.PlayStation,
                pending.AccountId,
                ReturnUrl: pending.ReturnUrl,
                Error: "Could not parse PlayStation account id.");
        }

        return PlatformOAuthPersistence.SaveLink(
            _identity,
            _credentials,
            _tokenProtector,
            pending.AccountId!,
            NlPlatform.PlayStation,
            accountId,
            profile?.Profile?.OnlineId,
            refreshToken,
            accessToken,
            expiresIn,
            null,
            pending.ReturnUrl);
    }

    private (string? AccountId, string? ReturnUrl, string? Error) ConsumeState(IReadOnlyDictionary<string, string> query)
    {
        if (!query.TryGetValue("state", out var state) || string.IsNullOrWhiteSpace(state))
        {
            return (null, null, "Missing OAuth state.");
        }

        var pending = _stateStore.Consume(state);
        if (pending is null)
        {
            return (null, null, "Invalid or expired OAuth state.");
        }

        if (query.TryGetValue("error", out var oauthError) && !string.IsNullOrWhiteSpace(oauthError))
        {
            var desc = query.TryGetValue("error_description", out var d) ? d : oauthError;
            return (pending.AccountId, pending.ReturnUrl, desc);
        }

        return (pending.AccountId, pending.ReturnUrl, null);
    }

    private sealed class PsnProfileEnvelope
    {
        [JsonPropertyName("profile")]
        public PsnProfile? Profile { get; set; }
    }

    private sealed class PsnProfile
    {
        [JsonPropertyName("accountId")]
        public string? AccountId { get; set; }

        [JsonPropertyName("onlineId")]
        public string? OnlineId { get; set; }
    }
}
