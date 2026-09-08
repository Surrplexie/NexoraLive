using NL.Server.Core;
using Xunit;

namespace NL.Server.Core.Tests;

public class PlayerSessionTrackerTests
{
    [Fact]
    public void GetStats_UnknownPlayer_ReturnsEmpty()
    {
        var tracker = new PlayerSessionTracker();
        Assert.Equal(PlayerStats.Empty, tracker.GetStats("Nobody"));
    }

    [Fact]
    public void RecordJoin_IncrementsJoinCountOnly()
    {
        var tracker = new PlayerSessionTracker();
        tracker.RecordJoin("Steve");
        var stats = tracker.RecordJoin("Steve");

        Assert.Equal(2, stats.JoinCount);
        Assert.Equal(0, stats.DeathCount);
        Assert.Equal(0, stats.AdvancementCount);
    }

    [Fact]
    public void RecordDeath_And_RecordAdvancement_TrackIndependently()
    {
        var tracker = new PlayerSessionTracker();
        tracker.RecordDeath("Steve");
        tracker.RecordDeath("Steve");
        tracker.RecordAdvancement("Steve");

        var stats = tracker.GetStats("Steve");
        Assert.Equal(2, stats.DeathCount);
        Assert.Equal(1, stats.AdvancementCount);
    }

    [Fact]
    public void Stats_AreIsolatedPerPlayer()
    {
        var tracker = new PlayerSessionTracker();
        tracker.RecordJoin("Steve");
        tracker.RecordJoin("Steve");
        tracker.RecordJoin("Alex");

        Assert.Equal(2, tracker.GetStats("Steve").JoinCount);
        Assert.Equal(1, tracker.GetStats("Alex").JoinCount);
    }
}
