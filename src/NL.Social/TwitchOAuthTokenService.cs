using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Identity;
using NL.Social.Core;

namespace NL.Social;

/// <summary>Refreshes and caches per-player Twitch user access tokens.</summary>
public sealed class TwitchOAuthTokenService
{
    private const string TokenEndpoint = "https://id.twitch.tv/oauth2/token";

    private readonly JsonTwitchOAuthCredentialStore _credentials;
    private readonly NlTokenProtector _tokenProtector;
    private readonly HttpClient _http;

    public TwitchOAuthTokenService(
        JsonTwitchOAuthCredentialStore credentials,
        NlTokenProtector? tokenProtector = null,
        HttpClient? http = null)
    {
        _credentials = credentials;
        _tokenProtector = tokenProtector ?? new NlTokenProtector();
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<string?> GetValidAccessTokenAsync(string playerId, CancellationToken cancellationToken = default)
    {
        var credential = _credentials.GetByPlayer(playerId);
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

        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var refreshToken = _tokenProtector.Unprotect(credential.ProtectedRefreshToken);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["client_secret"] = clientSecret.Trim(),
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        });

        using var response = await _http.PostAsync(TokenEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken);
        if (body?.AccessToken is null)
        {
            return null;
        }

        var updated = credential with
        {
            ProtectedAccessToken = _tokenProtector.Protect(body.AccessToken),
            AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, body.ExpiresIn - 60)),
            ProtectedRefreshToken = string.IsNullOrWhiteSpace(body.RefreshToken)
                ? credential.ProtectedRefreshToken
                : _tokenProtector.Protect(body.RefreshToken),
        };
        _credentials.Save(updated);
        return body.AccessToken;
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
}
