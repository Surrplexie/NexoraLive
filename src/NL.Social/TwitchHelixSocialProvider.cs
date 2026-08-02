using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Social.Core;

namespace NL.Social;

/// <summary>
/// Minimal Twitch Helix follow/sub checks when <c>TWITCH_CLIENT_ID</c> and
/// <c>TWITCH_ACCESS_TOKEN</c> are configured. Falls back to mock provider on errors.
/// </summary>
public sealed class TwitchHelixSocialProvider : ISocialRelationshipProvider
{
    private readonly HttpClient _http;
    private readonly ISocialRelationshipProvider _fallback;

    public TwitchHelixSocialProvider(ISocialRelationshipProvider fallback, HttpClient? http = null)
    {
        _fallback = fallback;
        _http = http ?? new HttpClient();
    }

    public async Task<SocialRelationshipStatus> GetStatusAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default)
    {
        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var accessToken = Environment.GetEnvironmentVariable("TWITCH_ACCESS_TOKEN");
        var broadcasterId = context.StreamerConfig.TwitchBroadcasterId;
        var viewerId = context.Links.TwitchUserId;

        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(broadcasterId)
            || string.IsNullOrWhiteSpace(viewerId))
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }

        try
        {
            using var followReq = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.twitch.tv/helix/channels/followers?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&user_id={Uri.EscapeDataString(viewerId)}");
            followReq.Headers.Add("Client-Id", clientId);
            followReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var subReq = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.twitch.tv/helix/subscriptions?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&user_id={Uri.EscapeDataString(viewerId)}");
            subReq.Headers.Add("Client-Id", clientId);
            subReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var followTask = _http.SendAsync(followReq, cancellationToken);
            var subTask = _http.SendAsync(subReq, cancellationToken);
            await Task.WhenAll(followTask, subTask);

            var followRes = await followTask;
            var subRes = await subTask;

            var isFollowing = false;
            if (followRes.IsSuccessStatusCode)
            {
                var followBody = await followRes.Content.ReadFromJsonAsync<TwitchDataEnvelope<TwitchFollow>>(cancellationToken);
                isFollowing = followBody?.Data?.Count > 0;
            }

            var isSubscribed = false;
            if (subRes.IsSuccessStatusCode)
            {
                var subBody = await subRes.Content.ReadFromJsonAsync<TwitchDataEnvelope<TwitchSub>>(cancellationToken);
                isSubscribed = subBody?.Data?.Count > 0;
            }

            var discord = await _fallback.GetStatusAsync(context, cancellationToken);

            return new SocialRelationshipStatus(
                isFollowing,
                isSubscribed,
                discord.IsDiscordMember,
                "twitch-helix");
        }
        catch
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }
    }

    private sealed class TwitchDataEnvelope<T>
    {
        [JsonPropertyName("data")]
        public List<T>? Data { get; set; }
    }

    private sealed class TwitchFollow;

    private sealed class TwitchSub;
}
