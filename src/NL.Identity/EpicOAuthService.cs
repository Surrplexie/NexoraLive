using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Identity.Core;

namespace NL.Identity;

/// <summary>Epic Account Services OAuth + account linking (Phase L.3).</summary>
public sealed class EpicOAuthService
{
    public const string DefaultScopes = "basic_profile";

    private const string AuthorizeEndpoint = "https://www.epicgames.com/id/authorize";
    private const string TokenEndpoint = "https://api.epicgames.dev/epic/oauth/v2/token";

    private readonly JsonOAuthStateStore _stateStore;
    private readonly JsonPlatformOAuthCredentialStore _credentials;
    private readonly NlIdentityService _identity;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public EpicOAuthService(
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

    public bool IsConfigured => PlatformOAuthEnv.HasPair("EPIC_CLIENT_ID", "EPIC_CLIENT_SECRET");

    public string BuildAuthorizeRedirect(string accountId, string? returnUrl, string publicBaseUrl)
    {
        var clientId = PlatformOAuthEnv.Get("EPIC_CLIENT_ID")!.Trim();
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/identity/oauth/epic/callback";
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
            return new PlatformOAuthCallbackResult(false, NlPlatform.Epic, Error: pending.Error);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return new PlatformOAuthCallbackResult(
                false, NlPlatform.Epic, pending.AccountId, ReturnUrl: pending.ReturnUrl, Error: "Missing authorization code.");
        }

        var clientId = PlatformOAuthEnv.Get("EPIC_CLIENT_ID")!.Trim();
        var clientSecret = PlatformOAuthEnv.Get("EPIC_CLIENT_SECRET")!.Trim();
        var redirectUri = $"{publicBaseUrl.TrimEnd('/')}/api/v1/identity/oauth/epic/callback";

        var (accessToken, refreshToken, expiresIn) = await PlatformOAuthHttp.ExchangeCodeAsync(
            _http,
            TokenEndpoint,
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code.Trim(),
                ["redirect_uri"] = redirectUri,
            },
            clientId,
            clientSecret,
            cancellationToken);

        if (accessToken is null || refreshToken is null)
        {
            return new PlatformOAuthCallbackResult(
                false, NlPlatform.Epic, pending.AccountId, ReturnUrl: pending.ReturnUrl, Error: "Epic token exchange failed.");
        }

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.epicgames.dev/epic/id/v2/accounts");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var userResponse = await _http.SendAsync(userRequest, cancellationToken);
        if (!userResponse.IsSuccessStatusCode)
        {
            return new PlatformOAuthCallbackResult(
                false, NlPlatform.Epic, pending.AccountId, ReturnUrl: pending.ReturnUrl, Error: "Failed to load Epic account profile.");
        }

        var accounts = await userResponse.Content.ReadFromJsonAsync<List<EpicAccount>>(cancellationToken);
        var account = accounts?.FirstOrDefault();
        if (account?.AccountId is null)
        {
            return new PlatformOAuthCallbackResult(
                false, NlPlatform.Epic, pending.AccountId, ReturnUrl: pending.ReturnUrl, Error: "Could not parse Epic account id.");
        }

        return PlatformOAuthPersistence.SaveLink(
            _identity,
            _credentials,
            _tokenProtector,
            pending.AccountId!,
            NlPlatform.Epic,
            account.AccountId,
            account.DisplayName ?? account.PreferredUsername,
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

    private sealed class EpicAccount
    {
        [JsonPropertyName("accountId")]
        public string? AccountId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("preferredUsername")]
        public string? PreferredUsername { get; set; }
    }
}
