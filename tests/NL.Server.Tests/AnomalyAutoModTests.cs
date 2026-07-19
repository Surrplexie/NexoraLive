using NL.Core;
using NL.Core.Sp;
using NL.Moderation.Core;
using NL.Server;
using NL.Server.Core;
using Xunit;

namespace NL.Server.Tests;

public class AnomalyAutoModTests
{
    [Fact]
    public async Task HighSeverityAnomalyBlock_IssuesGraylistHold()
    {
        var store = new InMemoryModerationStore();
        var profiles = new InMemorySpProfileRepository();
        profiles.GetOrCreate("Eve", "Eve");
        var moderation = new ModerationService(store, profiles);

        var decision = new HostedDecision(
            new SessionEvent(
                GameEvent.Create("anomalyImpossibleAction", ("anomaly.severity", 2)),
                "Eve"),
            ActionResult.Block("impossible action"));

        await NlSessionRunner.TryAnomalyAutoModAsync(
            moderation, "streamer-zed", decision, CancellationToken.None);

        Assert.Equal(SpStanding.Graylist, profiles.Find("Eve")!.GetRelationship("streamer-zed").Standing);
        var recent = await store.GetRecentAsync("streamer-zed", 10);
        Assert.Contains(recent, r => r.Kind == ModerationActionKind.GraylistHold);
    }

    [Fact]
    public async Task LowSeverityAnomaly_DoesNotAutoMod()
    {
        var store = new InMemoryModerationStore();
        var profiles = new InMemorySpProfileRepository();
        profiles.GetOrCreate("Eve", "Eve");
        var moderation = new ModerationService(store, profiles);

        var decision = new HostedDecision(
            new SessionEvent(
                GameEvent.Create("anomalyRateSpike", ("anomaly.severity", 1), ("anomaly.count", 8)),
                "Eve"),
            ActionResult.Block("rate spike"));

        await NlSessionRunner.TryAnomalyAutoModAsync(
            moderation, "streamer-zed", decision, CancellationToken.None);

        Assert.Equal(SpStanding.Normal, profiles.Find("Eve")!.GetRelationship("streamer-zed").Standing);
    }
}
