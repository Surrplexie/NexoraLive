using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Identity;
using NL.Social.Core;

namespace NL.Social;

public sealed record YouTubeOAuthCallbackResult(
    bool Success,
    string? PlayerId = null,
    string? YouTubeChannelId = null,
    string? YouTubeChannelTitle = null,
    string? ReturnUrl = null,
    string? Error = null);

/// <summary>Google OAuth 2.0 authorization-code flow for linking SP YouTube accounts (Phase M.3).</summary>
public sealed class YouTubeOAuthService
{
    public const string DefaultScopes = "https://www.googleapis.com/auth/youtube.readonly";

    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string ChannelsEndpoint = "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true";

    private readonly JsonSocialOAuthStateStore _stateStore;
    private readonly JsonYouTubeOAuthCredentialStore _credentials;
    private readonly JsonSpSocialLinkStore _links;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public YouTubeOAuthService(
        JsonSocialOAuthStateStore stateStore,
        JsonYouTubeOAuthCredentialStore credentials,
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
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET"));

    public string BuildAuthorizeRedirect(string playerId, string? returnUrl, string publicBaseUrl)
    {
        var clientId = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID")!.Trim();
        var callback = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/youtube/callback";
        var state = _stateStore.Create(playerId, returnUrl, TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = callback,
            ["response_type"] = "code",
            ["scope"] = DefaultScopes,
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
        };

        return AuthorizeEndpoint + "?" + string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<YouTubeOAuthCallbackResult> HandleCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!query.TryGetValue("state", out var state) || string.IsNullOrWhiteSpace(state))
        {
            return new YouTubeOAuthCallbackResult(false, Error: "Missing OAuth state.");
        }

        var pending = _stateStore.Consume(state);
        if (pending is null)
        {
            return new YouTubeOAuthCallbackResult(false, Error: "Invalid or expired OAuth state.");
        }

        if (query.TryGetValue("error", out var oauthError) && !string.IsNullOrWhiteSpace(oauthError))
        {
            var desc = query.TryGetValue("error_description", out var d) ? d : oauthError;
            return new YouTubeOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: desc);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return new YouTubeOAuthCallbackResult(false, PlayerId: pending.PlayerId, ReturnUrl: pending.ReturnUrl, Error: "Missing authorization code.");
        }

        var clientId = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return new YouTubeOAuthCallbackResult(false, Error: "YOUTUBE_CLIENT_ID and YOUTUBE_CLIENT_SECRET required.");
        }

        var redirectUri = $"{publicBaseUrl.TrimEnd('/')}/api/v1/social/oauth/youtube/callback";

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
                return new YouTubeOAuthCallbackResult(
                    false,
                    PlayerId: pending.PlayerId,
                    ReturnUrl: pending.ReturnUrl,
                    Error: $"Token exchange failed: {errBody}");
            }

            var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
            if (tokenBody?.AccessToken is null || tokenBody.RefreshToken is null)
            {
                return new YouTubeOAuthCallbackResult(false, Error: "Token response missing access or refresh token.");
            }

            using var channelRequest = new HttpRequestMessage(HttpMethod.Get, ChannelsEndpoint);
            channelRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenBody.AccessToken);

            using var channelResponse = await _http.SendAsync(channelRequest, cancellationToken);
            if (!channelResponse.IsSuccessStatusCode)
            {
                return new YouTubeOAuthCallbackResult(false, Error: "Failed to load YouTube channel profile.");
            }

            var channelBody = await channelResponse.Content.ReadFromJsonAsync<YouTubeListEnvelope<YouTubeChannel>>(cancellationToken);
            var channel = channelBody?.Items?.FirstOrDefault();
            if (channel?.Id is null)
            {
                return new YouTubeOAuthCallbackResult(false, Error: "Could not parse YouTube channel id.");
            }

            var title = channel.Snippet?.Title;
            var credential = new YouTubeOAuthCredential(
                pending.PlayerId,
                channel.Id,
                title,
                _tokenProtector.Protect(tokenBody.RefreshToken),
                _tokenProtector.Protect(tokenBody.AccessToken),
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenBody.ExpiresIn - 60)));

            _credentials.Save(credential);
            var existing = _links.GetOrDefault(pending.PlayerId);
            _links.Save(new SpSocialLinks(
                pending.PlayerId,
                existing.TwitchUserId,
                channel.Id,
                existing.KickUserId,
                existing.DiscordUserId));

            return new YouTubeOAuthCallbackResult(
                true,
                pending.PlayerId,
                channel.Id,
                title,
                pending.ReturnUrl);
        }
        catch (YouTubeLinkConflictException ex)
        {
            return new YouTubeOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                YouTubeChannelId: ex.YouTubeChannelId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
        catch (Exception ex)
        {
            return new YouTubeOAuthCallbackResult(
                false,
                PlayerId: pending.PlayerId,
                ReturnUrl: pending.ReturnUrl,
                Error: ex.Message);
        }
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class YouTubeListEnvelope<T>
    {
        [JsonPropertyName("items")]
        public List<T>? Items { get; set; }
    }

    private sealed class YouTubeChannel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("snippet")]
        public YouTubeSnippet? Snippet { get; set; }
    }

    private sealed class YouTubeSnippet
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }
}
