using NL.Core.Sp;
using NL.Social.Core;

namespace NL.Social;

/// <summary>
/// Hydrates <see cref="SpStreamerRelationship"/> follow/sub/discord flags from live platform
/// APIs (or mock fixtures) before <see cref="JoinEligibilityEngine"/> runs.
/// </summary>
public sealed class SocialGateService
{
    private readonly ISocialRelationshipProvider _provider;
    private readonly JsonSpSocialLinkStore _links;
    private readonly JsonStreamerSocialStore _streamers;
    private readonly SocialStatusCache _cache;

    public SocialGateService(
        ISocialRelationshipProvider provider,
        JsonSpSocialLinkStore links,
        JsonStreamerSocialStore streamers,
        SocialStatusCache cache)
    {
        _provider = provider;
        _links = links;
        _streamers = streamers;
        _cache = cache;
    }

    public SpSocialLinks ResolveLinks(string playerId, SocialLinkInput? input = null)
    {
        var existing = _links.GetOrDefault(playerId);
        if (input is null)
        {
            return existing;
        }

        var merged = new SpSocialLinks(
            playerId,
            input.TwitchUserId ?? existing.TwitchUserId,
            input.YouTubeChannelId ?? existing.YouTubeChannelId,
            input.KickUserId ?? existing.KickUserId,
            input.DiscordUserId ?? existing.DiscordUserId);

        return _links.Save(merged);
    }

    public async Task<SocialRelationshipStatus> RefreshRelationshipAsync(
        SpProfile profile,
        string streamerId,
        SpSocialLinks links,
        bool requireFollow,
        bool requireSubscription,
        bool requireDiscordMember,
        CancellationToken cancellationToken = default)
    {
        if (!requireFollow && !requireSubscription && !requireDiscordMember)
        {
            return SocialRelationshipStatus.Unknown;
        }

        var streamerConfig = _streamers.GetOrDefault(streamerId);
        var cacheKey = $"{streamerId}:{profile.Id}";
        if (_cache.TryGetRelationship(cacheKey, out var cached))
        {
            ApplyRelationship(profile, streamerId, cached);
            return cached;
        }

        var context = new SocialGateContext(
            streamerId,
            profile.Id,
            links,
            streamerConfig,
            requireFollow,
            requireSubscription,
            requireDiscordMember);

        var status = await _provider.GetStatusAsync(context, cancellationToken);
        _cache.SetRelationship(cacheKey, status);
        ApplyRelationship(profile, streamerId, status);
        return status;
    }

    public StreamerSocialConfig GetStreamerConfig(string streamerId) =>
        _streamers.GetOrDefault(streamerId);

    private static void ApplyRelationship(SpProfile profile, string streamerId, SocialRelationshipStatus status)
    {
        var current = profile.GetRelationship(streamerId);
        profile.SetRelationship(current with
        {
            IsFollowing = status.IsFollowing,
            IsSubscribed = status.IsSubscribed,
            IsDiscordMember = status.IsDiscordMember,
        });
    }
}

public sealed class SocialLinkInput
{
    public string? TwitchUserId { get; init; }

    public string? YouTubeChannelId { get; init; }

    public string? KickUserId { get; init; }

    public string? DiscordUserId { get; init; }

    public static SocialLinkInput? FromAdmitRequest(
        string? twitchUserId,
        string? youtubeChannelId,
        string? kickUserId,
        string? discordUserId,
        string? socialPlatform,
        string? socialUserId)
    {
        if (!string.IsNullOrWhiteSpace(twitchUserId)
            || !string.IsNullOrWhiteSpace(youtubeChannelId)
            || !string.IsNullOrWhiteSpace(kickUserId)
            || !string.IsNullOrWhiteSpace(discordUserId))
        {
            return new SocialLinkInput
            {
                TwitchUserId = twitchUserId,
                YouTubeChannelId = youtubeChannelId,
                KickUserId = kickUserId,
                DiscordUserId = discordUserId,
            };
        }

        if (string.IsNullOrWhiteSpace(socialPlatform) || string.IsNullOrWhiteSpace(socialUserId))
        {
            return null;
        }

        if (!NlSocialPlatformNames.TryParse(socialPlatform, out var platform))
        {
            return null;
        }

        return platform switch
        {
            NlSocialPlatform.Twitch => new SocialLinkInput { TwitchUserId = socialUserId },
            NlSocialPlatform.YouTube => new SocialLinkInput { YouTubeChannelId = socialUserId },
            NlSocialPlatform.Kick => new SocialLinkInput { KickUserId = socialUserId },
            NlSocialPlatform.Discord => new SocialLinkInput { DiscordUserId = socialUserId },
            _ => null,
        };
    }
}
