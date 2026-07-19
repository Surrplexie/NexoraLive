using NL.AntiCheat.Core;
using NL.Core;
using NL.Server.Core;
using Xunit;

namespace NL.AntiCheat.Core.Tests;

public class AnomalyEventMapperTests
{
    [Fact]
    public void ToSessionEvent_UsesWellKnownNameAndSeverityProps()
    {
        var signal = new AnomalySignal(
            AnomalyKind.ImpossibleAction,
            "Eve",
            "shoot",
            "shoot while dead",
            new Dictionary<string, double> { ["anomaly.playerAlive"] = 0 },
            DateTimeOffset.UtcNow);

        var session = AnomalyEventMapper.ToSessionEvent(signal);

        Assert.Equal(AnomalyEventNames.ImpossibleAction, session.Event.Name);
        Assert.Equal("Eve", session.PlayerName);
        Assert.Equal(2, session.Event.Properties["anomaly.severity"]);
        Assert.Equal(0, session.Event.Properties["anomaly.playerAlive"]);
    }
}

public class AnomalyDetectingEventSourceTests
{
    [Fact]
    public async Task YieldsOriginalEventThenMappedAnomalyEvents()
    {
        var t0 = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
        var inner = new ArrayEventSource(new[]
        {
            new SessionEvent(GameEvent.Simple("playerJoin"), "Eve", t0),
            new SessionEvent(GameEvent.Simple("playerDeath"), "Eve", t0.AddSeconds(1)),
            new SessionEvent(GameEvent.Create("shoot", ("weapon.damage", 99)), "Eve", t0.AddSeconds(2)),
        });

        var source = new AnomalyDetectingEventSource(inner, AnomalyPipeline.CreateDefault());
        var events = new List<SessionEvent>();
        await foreach (var evt in source.ReadEventsAsync(CancellationToken.None))
        {
            events.Add(evt);
        }

        Assert.Equal(4, events.Count); // 3 originals + 1 anomaly
        Assert.Equal("shoot", events[2].Event.Name);
        Assert.Equal(AnomalyEventNames.ImpossibleAction, events[3].Event.Name);
    }
}

public class AntiCheatHostIntegrationTests
{
    [Fact]
    public async Task SampleLikeFlow_RuleEngineBlocksAnomalies()
    {
        const string config = """
            event playerJoin:
                allow
            event playerDeath:
                allow
            event respawn:
                allow
            event shoot:
                allow
            event move:
                allow
            event anomalyImpossibleAction:
                block
            event anomalyRateSpike:
                if anomaly.count >= 8:
                    block
                else:
                    allow
            event anomalyTeleport:
                if anomaly.distance > 40:
                    block
                else:
                    allow
            """;

        var t0 = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        var inner = new ArrayEventSource(new[]
        {
            new SessionEvent(GameEvent.Simple("playerJoin"), "Eve", t0),
            new SessionEvent(GameEvent.Simple("playerDeath"), "Eve", t0.AddMilliseconds(3000)),
            new SessionEvent(GameEvent.Create("shoot", ("weapon.damage", 99)), "Eve", t0.AddMilliseconds(3100)),
            new SessionEvent(GameEvent.Create("respawn", ("player.health", 0)), "Eve", t0.AddMilliseconds(4000)),
            // 8 rapid shoots → rate spike
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5000)),
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5050)),
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5100)),
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5150)),
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5200)),
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5250)),
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5300)),
            new SessionEvent(GameEvent.Simple("shoot"), "Eve", t0.AddMilliseconds(5350)),
            new SessionEvent(GameEvent.Create("move", ("player.x", 10), ("player.y", 64), ("player.z", 10)), "Eve", t0.AddMilliseconds(6000)),
            new SessionEvent(GameEvent.Create("move", ("player.x", 200), ("player.y", 64), ("player.z", 10)), "Eve", t0.AddMilliseconds(6050)),
        });

        var source = new AnomalyDetectingEventSource(inner);
        var sink = new RecordingActionSink();
        var host = new NlServerHost(RuleEngine.FromSource(config), source, sink);
        await host.RunAsync(CancellationToken.None);

        var blockedAnomalies = host.Decisions
            .Where(d => d.SessionEvent.Event.Name.StartsWith("anomaly", StringComparison.Ordinal)
                        && d.Result.Decision == Decision.Block)
            .Select(d => d.SessionEvent.Event.Name)
            .ToList();

        Assert.Contains(AnomalyEventNames.ImpossibleAction, blockedAnomalies);
        Assert.Contains(AnomalyEventNames.RateSpike, blockedAnomalies);
        Assert.Contains(AnomalyEventNames.Teleport, blockedAnomalies);
        Assert.Equal(3, sink.Applied.Count);
    }
}

public class GenericJsonTsTests
{
    [Fact]
    public void TryParse_UnixMsTs_SetsTimestampUtc()
    {
        var session = NL.Server.Core.Generic.GenericJsonLineParser.TryParse(
            """{"event":"shoot","player":"Eve","ts":1700000003100,"props":{"weapon.damage":99}}""");

        Assert.NotNull(session);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_003_100), session!.TimestampUtc);
        Assert.Equal(99, session.Event.Properties["weapon.damage"]);
    }
}
