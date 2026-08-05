using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Identity;
using NL.Social.Core;

namespace NL.Social;

public sealed record TwitchOAuthCallbackResult(
    bool Success,
    string? PlayerId = null,
    string? TwitchUserId = null,
    string? TwitchLogin = null,
    string? ReturnUrl = null,
    string? Error = null);

/// <summary>Twitch OAuth 2.0 authorization-code flow for linking SP accounts (Phase M.1).</summary>
public sealed class TwitchOAuthService
{
    public const string DefaultScopes = "user:read:follows user:read:subscriptions";

    private const string AuthorizeEndpoint = "https://id.twitch.tv/oauth2/authorize";
    private const string TokenEndpoint = "https://id.twitch.tv/oauth2/token";
    private const string UsersEndpoint = "https://api.twitch.tv/helix/users";

    private readonly JsonSocialOAuthStateStore _stateStore;
    private readonly JsonTwitchOAuthCredentialStore _credentials;
    private readonly JsonSpSocialLinkStore _links;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public TwitchOAuthService(
        JsonSocialOAuthStateStore stateStore,
        JsonTwitchOAuthCredentialStore credentials,
        JsonSpSocialLinkStore links,
        NlTokenProtector? tokenProtector = null,
        HttpClient? http = null)
    {
        _stateStore = stateStore;
        _credentials = credentials;
        _links = links;
        _tokenProtector = tokenProtector ?? new NlTokenProtector();
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET"));

    public string BuildAuthorizeRedirect(string playerId, string? returnUrl, string publicBaseUrl)
    {
        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID")!.Trim();
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/twitch/callback";
        var state = _stateStore.Create(playerId, returnUrl, TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = callback,
            ["response_type"] = "code",
            ["scope"] = DefaultScopes,
            ["state"] = state,
            ["force_verify"] = "true",
        };

        return AuthorizeEndpoint + "?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<TwitchOAuthCallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!query.TryGetValue("state", out var state) || string.IsNullOrWhiteSpace(state))
        {
            return new TwitchOAuthCallbackResult(false, Error: "Missing OAuth state.");
        }

        var pending = _stateStore.Consume(state);
        if (pending is null)
        {
            return new TwitchOAuthCallbackResult(false, Error: "Invalid or expired OAuth state.");
        }

        if (query.TryGetValue("error", out var oauthError) && !string.IsNullOrWhiteSpace(oauthError))
        {
            var desc = query.TryGetValue("error_description", out var d) ? d : oauthError;
            return new TwitchOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: desc);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return new TwitchOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: "Missing authorization code.");
        }

        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return new TwitchOAuthCallbackResult(false, Error: "TWITCH_CLIENT_ID and TWITCH_CLIENT_SECRET required.");
        }

        var redirectUri = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/twitch/callback";

        try
        {
            using var tokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["client_secret"] = clientSecret.Trim(),
                ["code"] = code.Trim(),
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
            });

            using var tokenResponse = await _http.PostAsync(TokenEndpoint, tokenContent, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
                return new TwitchOAuthCallbackResult(
                    false,
                    PlayerId: pending.PlayerId,
                    ReturnUrl: pending.ReturnUrl,
                    Error: $"Token exchange failed: {errBody}");
            }

            var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken);
            if (tokenBody?.AccessToken is null || tokenBody.RefreshToken is null)
            {
                return new TwitchOAuthCallbackResult(false, Error: "Token response missing access or refresh token.");
            }

            using var userRequest = new HttpRequestMessage(HttpMethod.Get, UsersEndpoint);
            userRequest.Headers.Add("Client-Id", clientId.Trim());
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenBody.AccessToken);

            using var userResponse = await _http.SendAsync(userRequest, cancellationToken);
            if (!userResponse.IsSuccessStatusCode)
            {
                return new TwitchOAuthCallbackResult(false, Error: "Failed to load Twitch user profile.");
            }

            var userBody = await userResponse.Content.ReadFromJsonAsync<TwitchUsersEnvelope>(cancellationToken);
            var user = userBody?.Data?.FirstOrDefault();
            if (user?.Id is null)
            {
                return new TwitchOAuthCallbackResult(false, Error: "Could not parse Twitch user id.");
            }

            var credential = new TwitchOAuthCredential(
                pending.PlayerId,
                user.Id,
                user.Login,
                _tokenProtector.Protect(tokenBody.RefreshToken),
                _tokenProtector.Protect(tokenBody.AccessToken),
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenBody.ExpiresIn - 60)));

            _credentials.Save(credential);
            _links.Save(new SpSocialLinks(pending.PlayerId, TwitchUserId: user.Id));

            return new TwitchOAuthCallbackResult(
                true,
                pending.PlayerId,
                user.Id,
                user.Login,
                pending.ReturnUrl);
        }
        catch (TwitchLinkConflictException ex)
        {
            return new TwitchOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                TwitchUserId: ex.TwitchUserId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
        catch (Exception ex)
        {
            return new TwitchOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
    }

    private sealed class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class TwitchUsersEnvelope
    {
        [JsonPropertyName("data")]
        public List<TwitchUser>? Data { get; set; }
    }

    private sealed class TwitchUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("login")]
        public string? Login { get; set; }
    }
}
