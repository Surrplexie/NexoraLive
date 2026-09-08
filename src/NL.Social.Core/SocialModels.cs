namespace NL.Social.Core;

/// <summary>Streamer-connected channels used for follow/sub/live checks (Phase M).</summary>
public sealed record StreamerSocialConfig(
    string StreamerId,
    string? TwitchBroadcasterId = null,
    string? YouTubeChannelId = null,
    string? KickSlug = null,
    string? DiscordGuildId = null,
    bool RequireLiveToStart = false,
    NlSocialPlatform? LivePlatform = null)
{
    public static StreamerSocialConfig Empty(string streamerId) => new(streamerId);
}

/// <summary>Platform user ids linked to an NL SP profile.</summary>
public sealed record SpSocialLinks(
    string PlayerId,
    string? TwitchUserId = null,
    string? YouTubeChannelId = null,
    string? KickUserId = null,
    string? DiscordUserId = null);

/// <summary>Live follow/sub/discord status for one SP with one streamer.</summary>
public sealed record SocialRelationshipStatus(
    bool IsFollowing,
    bool IsSubscribed,
    bool IsDiscordMember,
    string Source = "unknown")
{
    public static SocialRelationshipStatus Unknown { get; } = new(false, false, false, "unknown");
}

public sealed record LiveStreamStatus(
    bool IsLive,
    NlSocialPlatform? Platform = null,
    string? Title = null,
    DateTimeOffset? CheckedAtUtc = null);

public sealed record SocialGateContext(
    string StreamerId,
    string PlayerId,
    SpSocialLinks Links,
    StreamerSocialConfig StreamerConfig,
    bool RequireFollow,
    bool RequireSubscription,
    bool RequireDiscordMember);
