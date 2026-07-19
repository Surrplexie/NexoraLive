using NL.Core;
using NL.Moderation.Core;
using Xunit;

namespace NL.Moderation.Tests;

public class JsonlModerationStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nl-moderation-test-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public async Task AppendAsync_ThenGetRecentAsync_RoundTripsRecord()
    {
        var store = new JsonlModerationStore(_path);
        var record = ModerationRecord.ForAutomaticDecision(
            "streamer-zed", "Alice", "shoot", Decision.Block, "too strong",
            "NL.Server:generic", DateTimeOffset.UtcNow);

        await store.AppendAsync(record);
        var recent = await store.GetRecentAsync("streamer-zed", 10);

        Assert.Single(recent);
        Assert.Equal(record.Id, recent[0].Id);
        Assert.Equal(record.PlayerName, recent[0].PlayerName);
        Assert.Equal(record.Decision, recent[0].Decision);
    }

    [Fact]
    public async Task AppendAsync_SurvivesAcrossStoreInstances()
    {
        var first = new JsonlModerationStore(_path);
        await first.AppendAsync(ModerationRecord.ForModAction(
            "streamer-zed", ModerationActionKind.Ban, "sp-1", "Alice", "cheating", "mod-erin", DateTimeOffset.UtcNow));

        var second = new JsonlModerationStore(_path);
        var recent = await second.GetRecentAsync("streamer-zed", 10);

        Assert.Single(recent);
        Assert.Equal(ModerationActionKind.Ban, recent[0].Kind);
    }

    [Fact]
    public async Task GetRecentAsync_OrdersNewestFirst_AndScopesToStreamer()
    {
        var store = new JsonlModerationStore(_path);
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await store.AppendAsync(ModerationRecord.ForModAction(
            "streamer-a", ModerationActionKind.Warning, "sp-1", "Alice", "r1", "mod", t0));
        await store.AppendAsync(ModerationRecord.ForModAction(
            "streamer-a", ModerationActionKind.Ban, "sp-1", "Alice", "r2", "mod", t0.AddMinutes(1)));
        await store.AppendAsync(ModerationRecord.ForModAction(
            "streamer-b", ModerationActionKind.Warning, "sp-2", "Bob", "r3", "mod", t0.AddMinutes(2)));

        var recent = await store.GetRecentAsync("streamer-a", 10);

        Assert.Equal(2, recent.Count);
        Assert.Equal(ModerationActionKind.Ban, recent[0].Kind);
        Assert.Equal(ModerationActionKind.Warning, recent[1].Kind);
    }

    [Fact]
    public async Task GetForPlayerAsync_ScopesToStreamerAndPlayer()
    {
        var store = new JsonlModerationStore(_path);
        await store.AppendAsync(ModerationRecord.ForModAction(
            "streamer-a", ModerationActionKind.Warning, "sp-1", "Alice", "r1", "mod", DateTimeOffset.UtcNow));
        await store.AppendAsync(ModerationRecord.ForModAction(
            "streamer-a", ModerationActionKind.Warning, "sp-2", "Bob", "r2", "mod", DateTimeOffset.UtcNow));

        var forSp1 = await store.GetForPlayerAsync("streamer-a", "sp-1");

        Assert.Single(forSp1);
        Assert.Equal("sp-1", forSp1[0].PlayerId);
    }
}
