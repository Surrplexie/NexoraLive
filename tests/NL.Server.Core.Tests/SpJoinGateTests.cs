using NL.Core;
using NL.Core.Sp;
using NL.Server.Core;
using Xunit;

namespace NL.Server.Core.Tests;

public class SpJoinGateTests
{
    private const string StreamerId = "streamer-zed";
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

    private static SpProfile Make(string id, SpStanding standing)
    {
        var profile = new SpProfile
        {
            Id = id,
            DisplayName = id,
            AccountCreatedAtUtc = Now.AddYears(-1),
        };
        profile.SetRelationship(new SpStreamerRelationship(StreamerId, standing));
        return profile;
    }

    [Fact]
    public void TryEvaluate_NonJoin_ReturnsNull()
    {
        var gate = new SpJoinGate(StreamerId, JoinRequirements.None, (id, _) => Make(id, SpStanding.Normal), () => Now);
        Assert.Null(gate.TryEvaluate(new SessionEvent(GameEvent.Simple("shoot"), "Eve")));
    }

    [Fact]
    public void TryEvaluate_Banned_Blocks()
    {
        var profiles = new Dictionary<string, SpProfile> { ["Eve"] = Make("Eve", SpStanding.Banned) };
        var gate = new SpJoinGate(
            StreamerId, JoinRequirements.None,
            (id, name) => profiles.TryGetValue(id, out var p) ? p : Make(id, SpStanding.Normal),
            () => Now);

        var outcome = gate.TryEvaluate(new SessionEvent(GameEvent.Simple("playerJoin"), "Eve"));

        Assert.NotNull(outcome);
        Assert.Equal(Decision.Block, outcome!.ActionResult.Decision);
        Assert.Equal(JoinDecision.Deny, outcome.JoinResult.Decision);
        Assert.Equal("Eve", outcome.PlayerId);
    }

    [Fact]
    public void TryEvaluate_Graylist_BlocksAsHold()
    {
        var profiles = new Dictionary<string, SpProfile> { ["Bob"] = Make("Bob", SpStanding.Graylist) };
        var gate = new SpJoinGate(
            StreamerId, JoinRequirements.None,
            (id, _) => profiles[id],
            () => Now);

        var outcome = gate.TryEvaluate(new SessionEvent(GameEvent.Simple("playerJoin"), "Bob"));

        Assert.NotNull(outcome);
        Assert.Equal(Decision.Block, outcome!.ActionResult.Decision);
        Assert.Equal(JoinDecision.Hold, outcome.JoinResult.Decision);
    }

    [Fact]
    public void TryEvaluate_Normal_Allows_ThenHostCanRunRules()
    {
        var gate = new SpJoinGate(
            StreamerId, JoinRequirements.None,
            (id, name) => Make(id, SpStanding.Normal),
            () => Now);

        var outcome = gate.TryEvaluate(new SessionEvent(GameEvent.Simple("playerJoin"), "Alice"));

        Assert.NotNull(outcome);
        Assert.Equal(Decision.Allow, outcome!.ActionResult.Decision);
        Assert.Equal(JoinDecision.Allow, outcome.JoinResult.Decision);
    }

    [Fact]
    public async Task Host_JoinGateDeny_SkipsRuleEngineAndAppliesBlock()
    {
        var gate = new SpJoinGate(
            StreamerId, JoinRequirements.None,
            (id, _) => Make(id, SpStanding.Banned),
            () => Now);

        const string config = """
            event playerJoin:
                warn "should not see this for banned"
                allow
            """;

        var source = new ArrayEventSource(new[]
        {
            new SessionEvent(GameEvent.Simple("playerJoin"), "Eve"),
        });
        var sink = new RecordingActionSink();
        var host = new NlServerHost(RuleEngine.FromSource(config), source, sink, gate);

        await host.RunAsync(CancellationToken.None);

        Assert.Single(host.Decisions);
        Assert.Equal(Decision.Block, host.Decisions[0].Result.Decision);
        Assert.DoesNotContain("should not see this", host.Decisions[0].Result.Message ?? "");
        Assert.Single(sink.Applied);
        Assert.Equal(JoinDecision.Deny, host.Decisions[0].JoinGate!.JoinResult.Decision);
    }
}
