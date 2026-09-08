using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Identity;
using NL.Social.Core;

namespace NL.Social;

public sealed record KickOAuthCallbackResult(
    bool Success,
    string? PlayerId = null,
    string? KickUserId = null,
    string? KickUsername = null,
    string? ReturnUrl = null,
    string? Error = null);

/// <summary>Kick OAuth 2.1 authorization-code flow with PKCE for linking SP accounts (Phase M.4).</summary>
public sealed class KickOAuthService
{
    public const string DefaultScopes = "user:read channel:read";

    private const string AuthorizeEndpoint = "https://id.kick.com/oauth/authorize";
    private const string TokenEndpoint = "https://id.kick.com/oauth/token";
    private const string UsersEndpoint = "https://api.kick.com/public/v1/users";

    private readonly JsonSocialOAuthStateStore _stateStore;
    private readonly JsonKickOAuthCredentialStore _credentials;
    private readonly JsonSpSocialLinkStore _links;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public KickOAuthService(
        JsonSocialOAuthStateStore stateStore,
        JsonKickOAuthCredentialStore credentials,
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
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KICK_CLIENT_ID"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KICK_CLIENT_SECRET"));

    public string BuildAuthorizeRedirect(string playerId, string? returnUrl, string publicBaseUrl)
    {
        var clientId = Environment.GetEnvironmentVariable("KICK_CLIENT_ID")!.Trim();
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/kick/callback";
        var verifier = KickPkceHelper.GenerateVerifier();
        var challenge = KickPkceHelper.ComputeChallenge(verifier);
        var state = _stateStore.Create(playerId, returnUrl, TimeSpan.FromMinutes(10), verifier);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = callback,
            ["response_type"] = "code",
            ["scope"] = DefaultScopes,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };

        return AuthorizeEndpoint + "?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<KickOAuthCallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!query.TryGetValue("state", out var state) || string.IsNullOrWhiteSpace(state))
        {
            return new KickOAuthCallbackResult(false, Error: "Missing OAuth state.");
        }

        var pending = _stateStore.Consume(state);
        if (pending is null)
        {
            return new KickOAuthCallbackResult(false, Error: "Invalid or expired OAuth state.");
        }

        if (string.IsNullOrWhiteSpace(pending.CodeVerifier))
        {
            return new KickOAuthCallbackResult(false, Error: "Missing PKCE verifier for Kick OAuth.");
        }

        if (query.TryGetValue("error", out var oauthError) && !string.IsNullOrWhiteSpace(oauthError))
        {
            var desc = query.TryGetValue("error_description", out var d) ? d : oauthError;
            return new KickOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: desc);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return new KickOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: "Missing authorization code.");
        }

        var clientId = Environment.GetEnvironmentVariable("KICK_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("KICK_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return new KickOAuthCallbackResult(false, Error: "KICK_CLIENT_ID and KICK_CLIENT_SECRET required.");
        }

        var redirectUri = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/kick/callback";

        try
        {
            using var tokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["client_secret"] = clientSecret.Trim(),
                ["code"] = code.Trim(),
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = pending.CodeVerifier,
            });

            using var tokenResponse = await _http.PostAsync(TokenEndpoint, tokenContent, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
                return new KickOAuthCallbackResult(
                    false,
                    PlayerId: pending.PlayerId,
                    ReturnUrl: pending.ReturnUrl,
                    Error: $"Token exchange failed: {errBody}");
            }

            var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<KickTokenResponse>(cancellationToken);
            if (tokenBody?.AccessToken is null || tokenBody.RefreshToken is null)
            {
                return new KickOAuthCallbackResult(false, Error: "Token response missing access or refresh token.");
            }

            using var userRequest = new HttpRequestMessage(HttpMethod.Get, UsersEndpoint);
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenBody.AccessToken);

            using var userResponse = await _http.SendAsync(userRequest, cancellationToken);
            if (!userResponse.IsSuccessStatusCode)
            {
                return new KickOAuthCallbackResult(false, Error: "Failed to load Kick user profile.");
            }

            var userBody = await userResponse.Content.ReadFromJsonAsync<KickApiEnvelope<List<KickUser>>>(cancellationToken);
            var user = userBody?.Data?.FirstOrDefault();
            if (user?.UserId is null)
            {
                return new KickOAuthCallbackResult(false, Error: "Could not parse Kick user id.");
            }

            var kickUserId = user.UserId.Value.ToString();
            var username = user.Name;
            var credential = new KickOAuthCredential(
                pending.PlayerId,
                kickUserId,
                username,
                _tokenProtector.Protect(tokenBody.RefreshToken),
                _tokenProtector.Protect(tokenBody.AccessToken),
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenBody.ExpiresIn - 60)));

            _credentials.Save(credential);
            var existing = _links.GetOrDefault(pending.PlayerId);
            _links.Save(new SpSocialLinks(
                pending.PlayerId,
                existing.TwitchUserId,
                existing.YouTubeChannelId,
                kickUserId,
                existing.DiscordUserId));

            return new KickOAuthCallbackResult(
                true,
                pending.PlayerId,
                kickUserId,
                username,
                pending.ReturnUrl);
        }
        catch (KickLinkConflictException ex)
        {
            return new KickOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                KickUserId: ex.KickUserId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
        catch (Exception ex)
        {
            return new KickOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
    }

    private sealed class KickTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class KickApiEnvelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class KickUser
    {
        [JsonPropertyName("user_id")]
        public long? UserId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
