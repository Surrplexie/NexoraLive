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

        if (mode == NlSocialMode.Live && !twitchLiveReady && !discordLiveReady)
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
            oauth = new
            {
                twitchAuthorize = "/api/v1/social/oauth/twitch/authorize",
                twitchCallback = "/api/v1/social/oauth/twitch/callback",
                twitchScopes = TwitchOAuthService.DefaultScopes,
                discordAuthorize = "/api/v1/social/oauth/discord/authorize",
                discordCallback = "/api/v1/social/oauth/discord/callback",
                discordScopes = DiscordOAuthService.DefaultScopes,
            },
            storePath = NlSocialPaths.Root,
            mockDataPath = NlSocialPaths.MockData,
            socialLinkPath = "/social-link.html",
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
        DiscordGuild = new DiscordGuildMemberService(DiscordTokenService);
        Cache = new SocialStatusCache(TimeSpan.FromSeconds(settings.CacheTtlSeconds));

        var mock = new MockSocialRelationshipProvider();
        LiveMonitor = mock;

        ISocialRelationshipProvider provider = settings.Mode switch
        {
            NlSocialMode.Off => new OffSocialRelationshipProvider(),
            NlSocialMode.Live => new LiveSocialRelationshipProvider(
                new TwitchHelixSocialProvider(mock, TwitchTokenService),
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
