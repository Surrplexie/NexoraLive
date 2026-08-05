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

        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
        var serverToken = Environment.GetEnvironmentVariable("TWITCH_ACCESS_TOKEN");
        var twitchLiveReady = !string.IsNullOrWhiteSpace(clientId)
            && (!string.IsNullOrWhiteSpace(clientSecret) || !string.IsNullOrWhiteSpace(serverToken));

        if (mode == NlSocialMode.Live && !twitchLiveReady)
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
        var clientId = Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET");
        var serverToken = Environment.GetEnvironmentVariable("TWITCH_ACCESS_TOKEN");

        return new
        {
            enabled = Enabled,
            mode = Mode.ToString(),
            cacheTtlSeconds = CacheTtlSeconds,
            liveCheckIntervalSeconds = LiveCheckIntervalSeconds,
            twitchConfigured = !string.IsNullOrWhiteSpace(clientId)
                && (!string.IsNullOrWhiteSpace(clientSecret) || !string.IsNullOrWhiteSpace(serverToken)),
            twitchOAuthConfigured = !string.IsNullOrWhiteSpace(clientId)
                && !string.IsNullOrWhiteSpace(clientSecret),
            oauth = new
            {
                twitchAuthorize = "/api/v1/social/oauth/twitch/authorize",
                twitchCallback = "/api/v1/social/oauth/twitch/callback",
                scopes = TwitchOAuthService.DefaultScopes,
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
        Cache = new SocialStatusCache(TimeSpan.FromSeconds(settings.CacheTtlSeconds));

        var mock = new MockSocialRelationshipProvider();
        LiveMonitor = mock;

        ISocialRelationshipProvider provider = settings.Mode switch
        {
            NlSocialMode.Off => new OffSocialRelationshipProvider(),
            NlSocialMode.Live => new TwitchHelixSocialProvider(mock, TwitchTokenService),
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
