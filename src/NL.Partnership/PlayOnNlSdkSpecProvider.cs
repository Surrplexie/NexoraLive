using NL.Partnership.Core;

namespace NL.Partnership;

public static class PlayOnNlSdkSpecProvider
{
    public static PlayOnNlSdkSpec Create(string httpBaseUrl)
    {
        var baseUrl = httpBaseUrl.Trim().TrimEnd('/');
        return new PlayOnNlSdkSpec
        {
            SpecVersion = "2026.1",
            Summary = "Play on NL — publisher menu integration for ownership proof, fork auth, and session disclaimers.",
            Ownership = new PlayOnNlOwnershipFlow(
                "Exchange platform session for a short-lived NL ownership token before admit.",
                $"{baseUrl}/api/v1/partnership/sdk/ownership-token",
                ["sub", "game_id", "app_id", "platform_user_id", "exp"]),
            ForkAuth = new PlayOnNlForkAuthFlow(
                "After admit, fetch session manifest for bridge URL and fork connect endpoint.",
                $"{baseUrl}/api/v1/session/manifest",
                $"{baseUrl}/api/v1/session/admit"),
            Disclaimer = new PlayOnNlDisclaimerFlow(
                "At-own-risk titles require one-time SP acknowledgment per gameId.",
                $"{baseUrl}/api/v1/partnership/legal/{{gameId}}",
                $"{baseUrl}/api/v1/partnership/acknowledge"),
            MenuEntry = new PlayOnNlMenuEntry(
                "Publisher implements an in-game button that opens NL join flow or deep-link.",
                "Play on NL",
                "Call ownership token endpoint, show disclaimer when tier=AtOwnRisk, then POST admit with token + ack flag."),
            DeepLink = new PlayOnNlDeepLink(
                "nlclient",
                "nlclient://join?streamer={streamerId}&game={gameId}&major={majorVersion}",
                "nlclient://join?streamer=default-streamer&game=hello-fork&major=1.0"),
        };
    }
}

public sealed class PublisherDashboardService
{
    private readonly IPublisherRegistry _publishers;
    private readonly IPublisherBanStore _bans;
    private readonly JsonPublisherSessionMetricsStore _metrics;

    public PublisherDashboardService(
        IPublisherRegistry publishers,
        IPublisherBanStore bans,
        JsonPublisherSessionMetricsStore metrics)
    {
        _publishers = publishers;
        _bans = bans;
        _metrics = metrics;
    }

    public PublisherDashboardSnapshot GetSnapshot(string publisherId)
    {
        var pub = _publishers.Get(publisherId)
            ?? throw new InvalidOperationException($"Unknown publisher '{publisherId}'.");
        var banCount = pub.Titles.Sum(t => _bans.ListForGame(t.GameId).Count);
        return new PublisherDashboardSnapshot(
            pub.PublisherId,
            pub.DisplayName,
            pub.Titles,
            _metrics.GetJoinCount(pub.PublisherId),
            banCount,
            DateTimeOffset.UtcNow);
    }
}
