using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Social.Core;

namespace NL.Social;

/// <summary>
/// YouTube Data API subscription checks using per-player OAuth tokens when available.
/// Maps free channel subscription to follow; paid membership is not available via viewer OAuth.
/// </summary>
public sealed class YouTubeDataSocialProvider : ISocialRelationshipProvider
{
    private readonly HttpClient _http;
    private readonly ISocialRelationshipProvider _fallback;
    private readonly YouTubeOAuthTokenService? _tokenService;

    public YouTubeDataSocialProvider(
        ISocialRelationshipProvider fallback,
        YouTubeOAuthTokenService? tokenService = null,
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
        var streamerChannelId = context.StreamerConfig.YouTubeChannelId;
        var viewerChannelId = context.Links.YouTubeChannelId;

        if (string.IsNullOrWhiteSpace(streamerChannelId)
            || string.IsNullOrWhiteSpace(viewerChannelId))
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
            var url = "https://www.googleapis.com/youtube/v3/subscriptions?part=id&mine=true&forChannelId="
                + Uri.EscapeDataString(streamerChannelId.Trim());

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return inner;
            }

            var body = await response.Content.ReadFromJsonAsync<YouTubeListEnvelope<YouTubeSubscription>>(cancellationToken);
            var isSubscribed = body?.Items?.Count > 0;

            return new SocialRelationshipStatus(
                inner.IsFollowing || isSubscribed,
                inner.IsSubscribed || isSubscribed,
                inner.IsDiscordMember,
                MergeSource(inner.Source, "youtube-oauth"));
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

    private sealed class YouTubeListEnvelope<T>
    {
        [JsonPropertyName("items")]
        public List<T>? Items { get; set; }
    }

    private sealed class YouTubeSubscription;

    private static string MergeSource(string innerSource, string platformSource)
    {
        if (string.IsNullOrWhiteSpace(innerSource) || innerSource == "unknown")
        {
            return platformSource;
        }

        return innerSource.Contains(platformSource, StringComparison.OrdinalIgnoreCase)
            ? innerSource
            : innerSource + "+" + platformSource;
    }
}
