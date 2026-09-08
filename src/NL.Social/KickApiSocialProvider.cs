using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Social.Core;

namespace NL.Social;

/// <summary>
/// Kick live relationship checks. OAuth linking is fully supported; follow/sub verification
/// merges with inner provider because Kick's public API has no viewer follow lookup endpoint.
/// </summary>
public sealed class KickApiSocialProvider : ISocialRelationshipProvider
{
    private readonly HttpClient _http;
    private readonly ISocialRelationshipProvider _fallback;
    private readonly KickOAuthTokenService? _tokenService;

    public KickApiSocialProvider(
        ISocialRelationshipProvider fallback,
        KickOAuthTokenService? tokenService = null,
        HttpClient? http = null)
    {
        _fallback = fallback;
        _tokenService = tokenService;
        _http = http ?? new HttpClient();
    }

    public async Task<SocialRelationshipStatus> GetStatusAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default)
    {
        var streamerSlug = context.StreamerConfig.KickSlug;
        var viewerKickUserId = context.Links.KickUserId;

        if (string.IsNullOrWhiteSpace(streamerSlug)
            || string.IsNullOrWhiteSpace(viewerKickUserId))
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }

        var accessToken = await ResolveAccessTokenAsync(context.PlayerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }

        try
        {
            var inner = await _fallback.GetStatusAsync(context, cancellationToken);

            // Validate token + resolve streamer channel; follow status still comes from inner/mock.
            using var channelRequest = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.kick.com/public/v1/channels?slug=" + Uri.EscapeDataString(streamerSlug.Trim()));
            channelRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var channelResponse = await _http.SendAsync(channelRequest, cancellationToken);
            if (!channelResponse.IsSuccessStatusCode)
            {
                return inner;
            }

            var channelBody = await channelResponse.Content.ReadFromJsonAsync<KickApiEnvelope<List<KickChannel>>>(cancellationToken);
            if (channelBody?.Data is null || channelBody.Data.Count == 0)
            {
                return inner;
            }

            var source = string.IsNullOrWhiteSpace(inner.Source) || inner.Source == "unknown"
                ? "kick-oauth"
                : inner.Source + "+kick-oauth";

            return inner with { Source = source };
        }
        catch
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }
    }

    private async Task<string?> ResolveAccessTokenAsync(string playerId, CancellationToken cancellationToken)
    {
        if (_tokenService is not null)
        {
            return await _tokenService.GetValidAccessTokenAsync(playerId, cancellationToken);
        }

        return null;
    }

    private sealed class KickApiEnvelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class KickChannel
    {
        [JsonPropertyName("broadcaster_user_id")]
        public long? BroadcasterUserId { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }
    }
}
