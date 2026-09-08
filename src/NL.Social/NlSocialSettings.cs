using NL.Social.Core;

namespace NL.Social;

public enum NlSocialMode
{
    Off,
    Mock,
    Live,
}

public sealed class NlSocialSettings
{
    public const string EnabledVariable = "NL_SOCIAL_ENABLED";
    public const string ModeVariable = "NL_SOCIAL_MODE";

    public bool Enabled { get; init; }

    public NlSocialMode Mode { get; init; } = NlSocialMode.Mock;

    public int CacheTtlSeconds { get; init; } = 300;

    public int LiveCheckIntervalSeconds { get; init; } = 60;

    public static NlSocialSettings LoadFromEnvironment()
    {
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable(EnabledVariable),
            "1",
            StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable(EnabledVariable),
                "true",
                StringComparison.OrdinalIgnoreCase);

        var modeRaw = Environment.GetEnvironmentVariable(ModeVariable)?.Trim();
        var mode = modeRaw?.ToLowerInvariant() switch
        {
            "off" => NlSocialMode.Off,
            "live" => NlSocialMode.Live,
            _ => NlSocialMode.Mock,
        };

        var twitchClientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var twitchClientSecret = Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
        var twitchServerToken = Environment.GetEnvironmentVariable("TWITCH_ACCESS_TOKEN");
        var twitchLiveReady = !string.IsNullOrWhiteSpace(twitchClientId)
            && (!string.IsNullOrWhiteSpace(twitchClientSecret) || !string.IsNullOrWhiteSpace(twitchServerToken));

        var discordClientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
        var discordClientSecret = Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET");
        var discordLiveReady = !string.IsNullOrWhiteSpace(discordClientId)
            && !string.IsNullOrWhiteSpace(discordClientSecret);

        var youtubeClientId = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID");
        var youtubeClientSecret = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET");
        var youtubeLiveReady = !string.IsNullOrWhiteSpace(youtubeClientId)
            && !string.IsNullOrWhiteSpace(youtubeClientSecret);

        var kickClientId = Environment.GetEnvironmentVariable("KICK_CLIENT_ID");
        var kickClientSecret = Environment.GetEnvironmentVariable("KICK_CLIENT_SECRET");
        var kickLiveReady = !string.IsNullOrWhiteSpace(kickClientId)
            && !string.IsNullOrWhiteSpace(kickClientSecret);

        if (mode == NlSocialMode.Live && !twitchLiveReady && !discordLiveReady && !youtubeLiveReady && !kickLiveReady)
        {
            mode = NlSocialMode.Mock;
        }

        var cacheTtl = int.TryParse(Environment.GetEnvironmentVariable("NL_SOCIAL_CACHE_TTL_SEC"), out var ttl)
            ? Math.Max(30, ttl)
            : 300;

        var liveInterval = int.TryParse(Environment.GetEnvironmentVariable("NL_LIVE_CHECK_INTERVAL_SEC"), out var live)
            ? Math.Max(15, live)
            : 60;

        return new NlSocialSettings
        {
            Enabled = enabled,
            Mode = mode,
            CacheTtlSeconds = cacheTtl,
            LiveCheckIntervalSeconds = liveInterval,
        };
    }

    public object ToPublicInfo()
    {
        var twitchClientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var twitchClientSecret = Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
        var twitchServerToken = Environment.GetEnvironmentVariable("TWITCH_ACCESS_TOKEN");
        var discordClientId = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
        var discordClientSecret = Environment.GetEnvironmentVariable("DISCORD_CLIENT_SECRET");
        var youtubeClientId = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_ID");
        var youtubeClientSecret = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET");
        var kickClientId = Environment.GetEnvironmentVariable("KICK_CLIENT_ID");
        var kickClientSecret = Environment.GetEnvironmentVariable("KICK_CLIENT_SECRET");

        return new
        {
            enabled = Enabled,
            mode = Mode.ToString(),
            cacheTtlSeconds = CacheTtlSeconds,
            liveCheckIntervalSeconds = LiveCheckIntervalSeconds,
            twitchConfigured = !string.IsNullOrWhiteSpace(twitchClientId)
                && (!string.IsNullOrWhiteSpace(twitchClientSecret) || !string.IsNullOrWhiteSpace(twitchServerToken)),
            twitchOAuthConfigured = !string.IsNullOrWhiteSpace(twitchClientId)
                && !string.IsNullOrWhiteSpace(twitchClientSecret),
            discordOAuthConfigured = !string.IsNullOrWhiteSpace(discordClientId)
                && !string.IsNullOrWhiteSpace(discordClientSecret),
            youtubeOAuthConfigured = !string.IsNullOrWhiteSpace(youtubeClientId)
                && !string.IsNullOrWhiteSpace(youtubeClientSecret),
            kickOAuthConfigured = !string.IsNullOrWhiteSpace(kickClientId)
                && !string.IsNullOrWhiteSpace(kickClientSecret),
            oauth = new
            {
                twitchAuthorize = "/api/v1/social/oauth/twitch/authorize",
                twitchCallback = "/api/v1/social/oauth/twitch/callback",
                twitchScopes = TwitchOAuthService.DefaultScopes,
                discordAuthorize = "/api/v1/social/oauth/discord/authorize",
                discordCallback = "/api/v1/social/oauth/discord/callback",
                discordScopes = DiscordOAuthService.DefaultScopes,
                youtubeAuthorize = "/api/v1/social/oauth/youtube/authorize",
                youtubeCallback = "/api/v1/social/oauth/youtube/callback",
                youtubeScopes = YouTubeOAuthService.DefaultScopes,
                kickAuthorize = "/api/v1/social/oauth/kick/authorize",
                kickCallback = "/api/v1/social/oauth/kick/callback",
                kickScopes = KickOAuthService.DefaultScopes,
            },
            storePath = NlSocialPaths.Root,
            mockDataPath = NlSocialPaths.MockData,
            socialLinkPath = "/social-link.html",
            liveSocialDogfoodPath = "/live-social-dogfood.html",
            dogfoodSocialSetup = "/api/v1/dogfood/social/setup",
        };
    }
}

public sealed class NlSocialHost
{
    public NlSocialHost(NlSocialSettings settings)
    {
        Settings = settings;
        NlSocialPaths.EnsureRoot();

        StreamerStore = new JsonStreamerSocialStore();
        LinkStore = new JsonSpSocialLinkStore();
        OAuthStates = new JsonSocialOAuthStateStore();
        TwitchCredentials = new JsonTwitchOAuthCredentialStore();
        TwitchTokenService = new TwitchOAuthTokenService(TwitchCredentials);
        TwitchOAuth = new TwitchOAuthService(OAuthStates, TwitchCredentials, LinkStore);
        DiscordCredentials = new JsonDiscordOAuthCredentialStore();
        DiscordTokenService = new DiscordOAuthTokenService(DiscordCredentials);
        DiscordOAuth = new DiscordOAuthService(OAuthStates, DiscordCredentials, LinkStore);
        YouTubeCredentials = new JsonYouTubeOAuthCredentialStore();
        YouTubeTokenService = new YouTubeOAuthTokenService(YouTubeCredentials);
        YouTubeOAuth = new YouTubeOAuthService(OAuthStates, YouTubeCredentials, LinkStore);
        KickCredentials = new JsonKickOAuthCredentialStore();
        KickTokenService = new KickOAuthTokenService(KickCredentials);
        KickOAuth = new KickOAuthService(OAuthStates, KickCredentials, LinkStore);
        DiscordGuild = new DiscordGuildMemberService(DiscordTokenService);
        Cache = new SocialStatusCache(TimeSpan.FromSeconds(settings.CacheTtlSeconds));

        var mock = new MockSocialRelationshipProvider();
        LiveMonitor = mock;

        ISocialRelationshipProvider provider = settings.Mode switch
        {
            NlSocialMode.Off => new OffSocialRelationshipProvider(),
            NlSocialMode.Live => new LiveSocialRelationshipProvider(
                new KickApiSocialProvider(
                    new YouTubeDataSocialProvider(
                        new TwitchHelixSocialProvider(mock, TwitchTokenService),
                        YouTubeTokenService),
                    KickTokenService),
                DiscordGuild),
            _ => mock,
        };

        if (settings.Mode != NlSocialMode.Off)
        {
            LiveMonitor = mock;
        }
        else
        {
            LiveMonitor = new OffLiveStreamMonitor();
        }

        RelationshipProvider = provider;
        Gate = new SocialGateService(provider, LinkStore, StreamerStore, Cache);
        _mockProvider = mock;
    }

    private readonly MockSocialRelationshipProvider _mockProvider;

    public void ReloadFixtures()
    {
        _mockProvider.Reload();
        StreamerStore.Reload();
        LinkStore.Reload();
        Cache.InvalidateAll();
    }

    public NlSocialSettings Settings { get; }

    public JsonStreamerSocialStore StreamerStore { get; }

    public JsonSpSocialLinkStore LinkStore { get; }

    public JsonSocialOAuthStateStore OAuthStates { get; }

    public JsonTwitchOAuthCredentialStore TwitchCredentials { get; }

    public TwitchOAuthTokenService TwitchTokenService { get; }

    public TwitchOAuthService TwitchOAuth { get; }

    public JsonDiscordOAuthCredentialStore DiscordCredentials { get; }

    public DiscordOAuthTokenService DiscordTokenService { get; }

    public DiscordOAuthService DiscordOAuth { get; }

    public JsonYouTubeOAuthCredentialStore YouTubeCredentials { get; }

    public YouTubeOAuthTokenService YouTubeTokenService { get; }

    public YouTubeOAuthService YouTubeOAuth { get; }

    public JsonKickOAuthCredentialStore KickCredentials { get; }

    public KickOAuthTokenService KickTokenService { get; }

    public KickOAuthService KickOAuth { get; }

    public DiscordGuildMemberService DiscordGuild { get; }

    public SocialStatusCache Cache { get; }

    public ISocialRelationshipProvider RelationshipProvider { get; }

    public ILiveStreamMonitor LiveMonitor { get; }

    public SocialGateService Gate { get; }
}

internal sealed class OffSocialRelationshipProvider : ISocialRelationshipProvider
{
    public Task<SocialRelationshipStatus> GetStatusAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(SocialRelationshipStatus.Unknown);
}

internal sealed class OffLiveStreamMonitor : ILiveStreamMonitor
{
    public Task<LiveStreamStatus> GetStatusAsync(
        StreamerSocialConfig config,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new LiveStreamStatus(true, null, "social-off", DateTimeOffset.UtcNow));
}
