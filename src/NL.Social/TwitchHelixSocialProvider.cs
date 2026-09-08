using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NL.Social.Core;

namespace NL.Social;

/// <summary>
/// Twitch Helix follow/sub checks using per-player OAuth tokens when available, otherwise
/// server <c>TWITCH_ACCESS_TOKEN</c>, then mock fallback.
/// </summary>
public sealed class TwitchHelixSocialProvider : ISocialRelationshipProvider
{
    private readonly HttpClient _http;
    private readonly ISocialRelationshipProvider _fallback;
    private readonly TwitchOAuthTokenService? _tokenService;

    public TwitchHelixSocialProvider(
        ISocialRelationshipProvider fallback,
        TwitchOAuthTokenService? tokenService = null,
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
        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var broadcasterId = context.StreamerConfig.TwitchBroadcasterId;
        var viewerId = context.Links.TwitchUserId;

        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(broadcasterId)
            || string.IsNullOrWhiteSpace(viewerId))
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }

        var accessToken = await ResolveAccessTokenAsync(context.PlayerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken.Token))
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }

        try
        {
            using var followReq = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.twitch.tv/helix/channels/followed?user_id={Uri.EscapeDataString(viewerId)}&broadcaster_id={Uri.EscapeDataString(broadcasterId)}");
            followReq.Headers.Add("Client-Id", clientId.Trim());
            followReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

            using var subReq = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.twitch.tv/helix/subscriptions/user?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&user_id={Uri.EscapeDataString(viewerId)}");
            subReq.Headers.Add("Client-Id", clientId.Trim());
            subReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

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
                accessToken.Source);
        }
        catch
        {
            return await _fallback.GetStatusAsync(context, cancellationToken);
        }
    }

    private async Task<(string? Token, string Source)> ResolveAccessTokenAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        if (_tokenService is not null)
        {
            var userToken = await _tokenService.GetValidAccessTokenAsync(playerId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(userToken))
            {
                return (userToken, "twitch-oauth");
            }
        }

        return (Environment.GetEnvironmentVariable("TWITCH_ACCESS_TOKEN"), "twitch-helix");
    }

    private sealed class TwitchDataEnvelope<T>
    {
        [JsonPropertyName("data")]
        public List<T>? Data { get; set; }
    }

    private sealed class TwitchFollow;

    private sealed class TwitchSub;
}
