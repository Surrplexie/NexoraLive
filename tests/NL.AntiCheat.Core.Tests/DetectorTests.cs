using NL.AntiCheat.Core;
using NL.AntiCheat.Core.Detectors;
using NL.Core;
using NL.Server.Core;
using Xunit;

namespace NL.AntiCheat.Core.Tests;

public class ImpossibleActionDetectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    private static SessionEvent Evt(string name, string player, DateTimeOffset at, params (string k, double v)[] props) =>
        new(GameEvent.Create(name, props), player, at);

    [Fact]
    public void ShootWhileDead_EmitsImpossibleAction()
    {
        var detector = new ImpossibleActionDetector();

        Assert.Empty(detector.Observe(Evt("playerJoin", "Eve", T0), T0));
        Assert.Empty(detector.Observe(Evt("playerDeath", "Eve", T0.AddSeconds(1)), T0.AddSeconds(1)));

        var signals = detector.Observe(Evt("shoot", "Eve", T0.AddSeconds(2), ("weapon.damage", 99)), T0.AddSeconds(2));

        Assert.Single(signals);
        Assert.Equal(AnomalyKind.ImpossibleAction, signals[0].Kind);
        Assert.Equal("Eve", signals[0].PlayerName);
    }

    [Fact]
    public void ShootAfterRespawn_IsClean()
    {
        var detector = new ImpossibleActionDetector();
        detector.Observe(Evt("playerDeath", "Eve", T0), T0);
        detector.Observe(Evt("respawn", "Eve", T0.AddSeconds(1)), T0.AddSeconds(1));

        Assert.Empty(detector.Observe(Evt("shoot", "Eve", T0.AddSeconds(2)), T0.AddSeconds(2)));
    }

    [Fact]
    public void AlivePlayer_NoSignal()
    {
        var detector = new ImpossibleActionDetector();
        detector.Observe(Evt("playerJoin", "Alice", T0), T0);

        Assert.Empty(detector.Observe(Evt("shoot", "Alice", T0.AddSeconds(1)), T0.AddSeconds(1)));
    }
}

public class RateSpikeDetectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EighthShotInOneSecond_EmitsRateSpike_Once()
    {
        var detector = new RateSpikeDetector(new AnomalyThresholds(RateSpikeMaxEvents: 8, RateSpikeWindowMs: 1000));
        AnomalySignal? signal = null;

        for (var i = 0; i < 9; i++)
        {
            var at = T0.AddMilliseconds(i * 50);
            var batch = detector.Observe(
                new SessionEvent(GameEvent.Simple("shoot"), "Eve", at), at);
            if (batch.Count > 0)
            {
                signal = batch[0];
            }
        }

        Assert.NotNull(signal);
        Assert.Equal(AnomalyKind.RateSpike, signal!.Kind);
        Assert.Equal(8, signal.Metrics["anomaly.count"]);

        // Still inside the spike window — already tripped, no second signal.
        var atAgain = T0.AddMilliseconds(450);
        Assert.Empty(detector.Observe(new SessionEvent(GameEvent.Simple("shoot"), "Eve", atAgain), atAgain));
    }

    [Fact]
    public void SpreadOutShots_NoSpike()
    {
        var detector = new RateSpikeDetector(new AnomalyThresholds(RateSpikeMaxEvents: 8, RateSpikeWindowMs: 1000));

        for (var i = 0; i < 8; i++)
        {
            var at = T0.AddSeconds(i * 2);
            Assert.Empty(detector.Observe(new SessionEvent(GameEvent.Simple("shoot"), "Alice", at), at));
        }
    }
}

public class TeleportDetectorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    private static SessionEvent Move(string player, DateTimeOffset at, double x, double z) =>
        new(GameEvent.Create("move", ("player.x", x), ("player.y", 64), ("player.z", z)), player, at);

    [Fact]
    public void LargeJump_EmitsTeleport()
    {
        var detector = new TeleportDetector(AnomalyThresholds.Default);

        Assert.Empty(detector.Observe(Move("Eve", T0, 0, 0), T0));
        var signals = detector.Observe(Move("Eve", T0.AddMilliseconds(50), 200, 0), T0.AddMilliseconds(50));

        Assert.Single(signals);
        Assert.Equal(AnomalyKind.Teleport, signals[0].Kind);
        Assert.True(signals[0].Metrics["anomaly.distance"] > 40);
    }

    [Fact]
    public void SmallStep_NoSignal()
    {
        var detector = new TeleportDetector(AnomalyThresholds.Default);

        Assert.Empty(detector.Observe(Move("Alice", T0, 0, 0), T0));
        Assert.Empty(detector.Observe(Move("Alice", T0.AddMilliseconds(500), 2, 1), T0.AddMilliseconds(500)));
    }
}
