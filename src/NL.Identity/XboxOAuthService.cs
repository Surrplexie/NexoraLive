using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NL.Identity.Core;

namespace NL.Identity;

/// <summary>Microsoft/Xbox Live OAuth + XUID linking (Phase L.3).</summary>
public sealed class XboxOAuthService
{
    public const string DefaultScopes = "XboxLive.signin XboxLive.offline_access";

    private const string AuthorizeEndpoint = "https://login.live.com/oauth20_authorize.srf";
    private const string TokenEndpoint = "https://login.live.com/oauth20_token.srf";

    private readonly JsonOAuthStateStore _stateStore;
    private readonly JsonPlatformOAuthCredentialStore _credentials;
    private readonly NlIdentityService _identity;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public XboxOAuthService(
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

    public bool IsConfigured => PlatformOAuthEnv.HasPair("XBOX_CLIENT_ID", "XBOX_CLIENT_SECRET")
        || PlatformOAuthEnv.HasPair("MICROSOFT_CLIENT_ID", "MICROSOFT_CLIENT_SECRET");

    public string BuildAuthorizeRedirect(string accountId, string? returnUrl, string publicBaseUrl)
    {
        var clientId = ResolveClientId();
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/identity/oauth/xbox/callback";
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
            return new PlatformOAuthCallbackResult(false, NlPlatform.Xbox, Error: pending.Error);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return new PlatformOAuthCallbackResult(
                false, NlPlatform.Xbox, pending.AccountId, ReturnUrl: pending.ReturnUrl, Error: "Missing authorization code.");
        }

        var clientId = ResolveClientId();
        var clientSecret = ResolveClientSecret();
        var redirectUri = $"{publicBaseUrl.TrimEnd('/')}/api/v1/identity/oauth/xbox/callback";

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
                false, NlPlatform.Xbox, pending.AccountId, ReturnUrl: pending.ReturnUrl, Error: "Xbox token exchange failed.");
        }

        var xboxUser = await XboxLiveAuthHelper.ResolveXuidAsync(_http, accessToken, cancellationToken);
        if (xboxUser?.Xuid is null)
        {
            return new PlatformOAuthCallbackResult(
                false, NlPlatform.Xbox, pending.AccountId, ReturnUrl: pending.ReturnUrl, Error: "Could not resolve Xbox XUID.");
        }

        var metadata = JsonSerializer.Serialize(new { xuid = xboxUser.Xuid, gamertag = xboxUser.Gamertag });
        return PlatformOAuthPersistence.SaveLink(
            _identity,
            _credentials,
            _tokenProtector,
            pending.AccountId!,
            NlPlatform.Xbox,
            xboxUser.Xuid,
            xboxUser.Gamertag,
            refreshToken,
            accessToken,
            expiresIn,
            metadata,
            pending.ReturnUrl);
    }

    private static string ResolveClientId() =>
        PlatformOAuthEnv.Get("XBOX_CLIENT_ID") ?? PlatformOAuthEnv.Get("MICROSOFT_CLIENT_ID") ?? "";

    private static string ResolveClientSecret() =>
        PlatformOAuthEnv.Get("XBOX_CLIENT_SECRET") ?? PlatformOAuthEnv.Get("MICROSOFT_CLIENT_SECRET") ?? "";

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
}

internal static class XboxLiveAuthHelper
{
    public static async Task<XboxProfile?> ResolveXuidAsync(
        HttpClient http,
        string microsoftAccessToken,
        CancellationToken cancellationToken)
    {
        var userToken = await AuthenticateUserAsync(http, microsoftAccessToken, cancellationToken);
        if (userToken is null)
        {
            return null;
        }

        var xsts = await AuthorizeXstsAsync(http, userToken, cancellationToken);
        if (xsts is null)
        {
            return null;
        }

        return new XboxProfile(xsts.Xuid, xsts.Gamertag);
    }

    public static async Task<XboxAuthTokens?> BuildTitleHubTokensAsync(
        HttpClient http,
        string microsoftAccessToken,
        CancellationToken cancellationToken)
    {
        var userToken = await AuthenticateUserAsync(http, microsoftAccessToken, cancellationToken);
        if (userToken is null)
        {
            return null;
        }

        var xsts = await AuthorizeXstsAsync(http, userToken, cancellationToken);
        return xsts;
    }

    private static async Task<string?> AuthenticateUserAsync(
        HttpClient http,
        string microsoftAccessToken,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={microsoftAccessToken}",
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://user.auth.xboxlive.com/user/authenticate")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return doc.TryGetProperty("Token", out var token) ? token.GetString() : null;
    }

    private static async Task<XboxAuthTokens?> AuthorizeXstsAsync(
        HttpClient http,
        string userToken,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            RelyingParty = "http://xboxlive.com",
            TokenType = "JWT",
            Properties = new
            {
                UserTokens = new[] { userToken },
                SandboxId = "RETAIL",
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://xsts.auth.xboxlive.com/xsts/authorize")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = doc.TryGetProperty("Token", out var t) ? t.GetString() : null;
        if (token is null)
        {
            return null;
        }

        string? xuid = null;
        string? gamertag = null;
        string? uhs = null;
        if (doc.TryGetProperty("DisplayClaims", out var claims)
            && claims.TryGetProperty("xui", out var xui)
            && xui.GetArrayLength() > 0)
        {
            var first = xui[0];
            xuid = first.TryGetProperty("xid", out var xid) ? xid.GetString() : null;
            gamertag = first.TryGetProperty("gtg", out var gtg) ? gtg.GetString() : null;
            uhs = first.TryGetProperty("uhs", out var u) ? u.GetString() : null;
        }

        if (xuid is null || uhs is null)
        {
            return null;
        }

        return new XboxAuthTokens(uhs, token, xuid, gamertag);
    }

    internal sealed record XboxProfile(string Xuid, string? Gamertag);

    internal sealed record XboxAuthTokens(string UserHash, string XstsToken, string Xuid, string? Gamertag);
}
