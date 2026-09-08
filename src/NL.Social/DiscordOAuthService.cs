using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Identity;
using NL.Social.Core;

namespace NL.Social;

public sealed record DiscordOAuthCallbackResult(
    bool Success,
    string? PlayerId = null,
    string? DiscordUserId = null,
    string? DiscordUsername = null,
    string? ReturnUrl = null,
    string? Error = null);

/// <summary>Discord OAuth 2.0 authorization-code flow for linking SP accounts (Phase M.2).</summary>
public sealed class DiscordOAuthService
{
    public const string DefaultScopes = "identify guilds.members.read";

    private const string AuthorizeEndpoint = "https://discord.com/api/oauth2/authorize";
    private const string TokenEndpoint = "https://discord.com/api/oauth2/token";
    private const string UserEndpoint = "https://discord.com/api/users/@me";

    private readonly JsonSocialOAuthStateStore _stateStore;
    private readonly JsonDiscordOAuthCredentialStore _credentials;
    private readonly JsonSpSocialLinkStore _links;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public DiscordOAuthService(
        JsonSocialOAuthStateStore stateStore,
        JsonDiscordOAuthCredentialStore credentials,
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
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET"));

    public string BuildAuthorizeRedirect(string playerId, string? returnUrl, string publicBaseUrl)
    {
        var clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID")!.Trim();
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/discord/callback";
        var state = _stateStore.Create(playerId, returnUrl, TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = callback,
            ["response_type"] = "code",
            ["scope"] = DefaultScopes,
            ["state"] = state,
            ["prompt"] = "consent",
        };

        return AuthorizeEndpoint + "?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<DiscordOAuthCallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!query.TryGetValue("state", out var state) || string.IsNullOrWhiteSpace(state))
        {
            return new DiscordOAuthCallbackResult(false, Error: "Missing OAuth state.");
        }

        var pending = _stateStore.Consume(state);
        if (pending is null)
        {
            return new DiscordOAuthCallbackResult(false, Error: "Invalid or expired OAuth state.");
        }

        if (query.TryGetValue("error", out var oauthError) && !string.IsNullOrWhiteSpace(oauthError))
        {
            var desc = query.TryGetValue("error_description", out var d) ? d : oauthError;
            return new DiscordOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: desc);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return new DiscordOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: "Missing authorization code.");
        }

        var clientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return new DiscordOAuthCallbackResult(false, Error: "DISCORD_CLIENT_ID and DISCORD_CLIENT_SECRET required.");
        }

        var redirectUri = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/discord/callback";

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
                return new DiscordOAuthCallbackResult(
                    false,
                    PlayerId: pending.PlayerId,
                    ReturnUrl: pending.ReturnUrl,
                    Error: $"Token exchange failed: {errBody}");
            }

            var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<DiscordTokenResponse>(cancellationToken);
            if (tokenBody?.AccessToken is null || tokenBody.RefreshToken is null)
            {
                return new DiscordOAuthCallbackResult(false, Error: "Token response missing access or refresh token.");
            }

            using var userRequest = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenBody.AccessToken);

            using var userResponse = await _http.SendAsync(userRequest, cancellationToken);
            if (!userResponse.IsSuccessStatusCode)
            {
                return new DiscordOAuthCallbackResult(false, Error: "Failed to load Discord user profile.");
            }

            var user = await userResponse.Content.ReadFromJsonAsync<DiscordUser>(cancellationToken);
            if (user?.Id is null)
            {
                return new DiscordOAuthCallbackResult(false, Error: "Could not parse Discord user id.");
            }

            var username = user.GlobalName ?? user.Username;
            var credential = new DiscordOAuthCredential(
                pending.PlayerId,
                user.Id,
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
                existing.KickUserId,
                user.Id));

            return new DiscordOAuthCallbackResult(
                true,
                pending.PlayerId,
                user.Id,
                username,
                pending.ReturnUrl);
        }
        catch (DiscordLinkConflictException ex)
        {
            return new DiscordOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                DiscordUserId: ex.DiscordUserId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
        catch (Exception ex)
        {
            return new DiscordOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
    }

    private sealed class DiscordTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class DiscordUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("global_name")]
        public string? GlobalName { get; set; }
    }
}
