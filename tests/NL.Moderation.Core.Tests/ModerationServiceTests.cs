using NL.Core;
using NL.Core.Sp;
using NL.Moderation.Core;
using Xunit;

namespace NL.Moderation.Core.Tests;

public class ModerationServiceTests
{
    private const string StreamerId = "streamer-zed";
    private const string ModId = "mod-erin";
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

    private static ModerationService NewService(out ISpProfileRepository profiles, out IModerationStore store)
    {
        store = new InMemoryModerationStore();
        profiles = new InMemorySpProfileRepository();
        return new ModerationService(store, profiles, clock: () => Now);
    }

    /// <summary>A clock that ticks forward by a second on every read — needed whenever a test
    /// issues several actions and asserts on their relative (newest-first) ordering, since a
    /// frozen clock would give every record the same timestamp.</summary>
    private static ModerationService NewServiceWithTickingClock(
        out ISpProfileRepository profiles, out IModerationStore store)
    {
        store = new InMemoryModerationStore();
        profiles = new InMemorySpProfileRepository();
        var tick = 0;
        return new ModerationService(store, profiles, clock: () => Now.AddSeconds(tick++));
    }

    private static ModerationService NewServiceWithProfile(string playerId, out ISpProfileRepository profiles)
    {
        var service = NewService(out profiles, out _);
        profiles.GetOrCreate(playerId, "Test SP");
        return service;
    }

    [Fact]
    public async Task RecordAutomaticDecisionAsync_AppendsToStoreAsAuditTrail()
    {
        var service = NewService(out _, out _);
        var gameEvent = new GameEvent("shoot", new Dictionary<string, double> { ["weapon.damage"] = 12 });

        await service.RecordAutomaticDecisionAsync(
            StreamerId, "Alice", gameEvent, ActionResult.Block("too strong"), "NL.Server:generic");

        var recent = await service.GetRecentActionsAsync(StreamerId);
        Assert.Single(recent);
        Assert.Equal(ModerationActionKind.AutomaticDecision, recent[0].Kind);
        Assert.Equal("Alice", recent[0].PlayerName);
        Assert.Equal(Decision.Block, recent[0].Decision);
        Assert.Null(recent[0].IssuedBy);
    }

    [Fact]
    public async Task IssueWarningAsync_UnknownPlayer_Throws()
    {
        var service = NewService(out _, out _);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IssueWarningAsync(StreamerId, "sp-ghost", ModId, "spam"));
    }

    [Fact]
    public async Task IssueWarningAsync_AddsOffenseAndLogsRecord_ButDoesNotChangeStanding()
    {
        var service = NewServiceWithProfile("sp-1", out var profiles);

        var offense = await service.IssueWarningAsync(StreamerId, "sp-1", ModId, "trash talk");

        Assert.Equal(StreamerId, offense.StreamerId);
        Assert.Equal(ModId, offense.IssuedBy);

        var profile = profiles.Find("sp-1")!;
        Assert.Single(profile.Offenses);
        Assert.Equal(SpStanding.Normal, profile.GetRelationship(StreamerId).Standing);

        var recent = await service.GetRecentActionsAsync(StreamerId);
        Assert.Single(recent);
        Assert.Equal(ModerationActionKind.Warning, recent[0].Kind);
        Assert.Equal(ModId, recent[0].IssuedBy);
    }

    [Fact]
    public async Task IssueBanAsync_SetsStandingToBanned_AndDeniesJoinViaJoinEligibilityEngine()
    {
        var service = NewServiceWithProfile("sp-2", out var profiles);

        await service.IssueBanAsync(StreamerId, "sp-2", ModId, "cheating");

        var profile = profiles.Find("sp-2")!;
        Assert.Equal(SpStanding.Banned, profile.GetRelationship(StreamerId).Standing);
        Assert.Single(profile.Offenses);

        // Cross-phase integration: Phase 4's ban must be enforced by Phase 2's join engine.
        var joinResult = JoinEligibilityEngine.Evaluate(profile, StreamerId, JoinRequirements.None, Now);
        Assert.Equal(JoinDecision.Deny, joinResult.Decision);
    }

    [Fact]
    public async Task IssueGraylistHoldAsync_SetsStandingToGraylist_NoOffenseRecorded()
    {
        var service = NewServiceWithProfile("sp-3", out var profiles);

        await service.IssueGraylistHoldAsync(StreamerId, "sp-3", ModId, "under investigation");

        var profile = profiles.Find("sp-3")!;
        Assert.Equal(SpStanding.Graylist, profile.GetRelationship(StreamerId).Standing);
        Assert.Empty(profile.Offenses);

        var joinResult = JoinEligibilityEngine.Evaluate(profile, StreamerId, JoinRequirements.None, Now);
        Assert.Equal(JoinDecision.Hold, joinResult.Decision);
    }

    [Fact]
    public async Task ClearStandingAsync_ResetsToNormal_KeepsPastOffenses()
    {
        var store = new InMemoryModerationStore();
        var profiles = (ISpProfileRepository)new InMemorySpProfileRepository();
        profiles.GetOrCreate("sp-4", "Test SP");
        var tick = 0;
        var service = new ModerationService(store, profiles, clock: () => Now.AddSeconds(tick++));
        await service.IssueBanAsync(StreamerId, "sp-4", ModId, "first offense");

        await service.ClearStandingAsync(StreamerId, "sp-4", ModId, "appeal accepted");

        var profile = profiles.Find("sp-4")!;
        Assert.Equal(SpStanding.Normal, profile.GetRelationship(StreamerId).Standing);
        Assert.Single(profile.Offenses); // offense stays on record

        var recent = await service.GetRecentActionsAsync(StreamerId);
        Assert.Equal(2, recent.Count);
        Assert.Equal(ModerationActionKind.StandingCleared, recent[0].Kind); // newest first
    }

    [Fact]
    public void GetOffenseHistory_ReturnsNull_ForUnknownPlayer()
    {
        var service = NewService(out _, out _);

        Assert.Null(service.GetOffenseHistory(StreamerId, "sp-ghost"));
    }

    [Fact]
    public async Task GetOffenseHistory_ScopesOffensesToStreamer_AndCountsActiveOnes()
    {
        var service = NewServiceWithProfile("sp-5", out _);
        await service.IssueWarningAsync(StreamerId, "sp-5", ModId, "warning 1");
        await service.IssueWarningAsync("other-streamer", "sp-5", ModId, "unrelated");

        var history = service.GetOffenseHistory(StreamerId, "sp-5");

        Assert.NotNull(history);
        Assert.Single(history!.Offenses);
        Assert.Equal(1, history.ActiveOffenseCount);
        Assert.Equal(SpStanding.Normal, history.Standing);
    }

    [Fact]
    public async Task GetRecentActionsAsync_OrdersNewestFirst_AndRespectsCount()
    {
        var service = NewServiceWithTickingClock(out var profiles, out _);
        profiles.GetOrCreate("sp-6", "SixthSP");

        await service.IssueWarningAsync(StreamerId, "sp-6", ModId, "a");
        await service.IssueGraylistHoldAsync(StreamerId, "sp-6", ModId, "b");
        await service.ClearStandingAsync(StreamerId, "sp-6", ModId, "c");

        var recent = await service.GetRecentActionsAsync(StreamerId, count: 2);

        Assert.Equal(2, recent.Count);
        Assert.Equal(ModerationActionKind.StandingCleared, recent[0].Kind);
        Assert.Equal(ModerationActionKind.GraylistHold, recent[1].Kind);
    }

    [Fact]
    public async Task GetPlayerActionsAsync_ScopesToStreamerAndPlayer()
    {
        var service = NewService(out var profiles, out _);
        profiles.GetOrCreate("sp-7", "SeventhSP");
        profiles.GetOrCreate("sp-8", "EighthSP");

        await service.IssueWarningAsync(StreamerId, "sp-7", ModId, "warn 7");
        await service.IssueWarningAsync(StreamerId, "sp-8", ModId, "warn 8");

        var forSp7 = await service.GetPlayerActionsAsync(StreamerId, "sp-7");

        Assert.Single(forSp7);
        Assert.Equal("sp-7", forSp7[0].PlayerId);
    }
}
