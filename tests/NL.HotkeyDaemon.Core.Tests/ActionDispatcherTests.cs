using NL.Core;
using NL.HotkeyDaemon.Core;
using Xunit;

namespace NL.HotkeyDaemon.Core.Tests;

public class ActionDispatcherTests
{
    private const string Config = """
        event toggleMic:
            block

        event announce:
            allow
        """;

    private static RuleEngine BuildEngine() => RuleEngine.FromSource(Config);

    [Fact]
    public void Dispatch_ActionWithNoAuthoredRule_DefaultsToPerform()
    {
        var dispatcher = new ActionDispatcher(BuildEngine());
        var decision = dispatcher.Dispatch("somethingUnconfigured");

        Assert.Equal(DispatchOutcome.Perform, decision.Outcome);
        Assert.True(decision.ShouldPerform);
    }

    [Fact]
    public void Dispatch_ActionBlockedByRule_ReturnsSkipWithMessage()
    {
        var dispatcher = new ActionDispatcher(BuildEngine());
        var decision = dispatcher.Dispatch("toggleMic");

        Assert.Equal(DispatchOutcome.Skip, decision.Outcome);
        Assert.False(decision.ShouldPerform);
    }

    [Fact]
    public void Dispatch_ActionAllowedByRule_ReturnsPerform()
    {
        var dispatcher = new ActionDispatcher(BuildEngine());
        var decision = dispatcher.Dispatch("announce");

        Assert.True(decision.ShouldPerform);
    }

    [Fact]
    public void Dispatch_WhenDisabled_SkipsRegularActionsWithoutConsultingEngine()
    {
        var dispatcher = new ActionDispatcher(BuildEngine());
        dispatcher.Dispatch(ActionDispatcher.ToggleNlEventsAction); // flips Enabled to false
        Assert.False(dispatcher.Enabled);

        // "announce" is explicitly allowed by the rule, but should still be skipped because
        // the whole daemon is disabled.
        var decision = dispatcher.Dispatch("announce");

        Assert.Equal(DispatchOutcome.Skip, decision.Outcome);
        Assert.Contains("disabled", decision.Message);
    }

    [Fact]
    public void Dispatch_ToggleAction_FlipsEnabledStateEachTime()
    {
        var dispatcher = new ActionDispatcher(BuildEngine());
        Assert.True(dispatcher.Enabled);

        var first = dispatcher.Dispatch(ActionDispatcher.ToggleNlEventsAction);
        Assert.True(first.ShouldPerform);
        Assert.False(dispatcher.Enabled);

        var second = dispatcher.Dispatch(ActionDispatcher.ToggleNlEventsAction);
        Assert.True(second.ShouldPerform);
        Assert.True(dispatcher.Enabled);
    }

    [Fact]
    public void Dispatch_ToggleAction_StillWorksWhileDisabled()
    {
        var dispatcher = new ActionDispatcher(BuildEngine());
        dispatcher.Dispatch(ActionDispatcher.ToggleNlEventsAction);
        Assert.False(dispatcher.Enabled);

        var decision = dispatcher.Dispatch(ActionDispatcher.ToggleNlEventsAction);

        Assert.True(decision.ShouldPerform);
        Assert.True(dispatcher.Enabled);
    }

    [Fact]
    public void Dispatch_ToggleAction_BlockedByRule_DoesNotFlipState()
    {
        const string source = """
            event toggleNlEvents:
                block
            """;
        var dispatcher = new ActionDispatcher(RuleEngine.FromSource(source));

        var decision = dispatcher.Dispatch(ActionDispatcher.ToggleNlEventsAction);

        Assert.Equal(DispatchOutcome.Skip, decision.Outcome);
        Assert.True(dispatcher.Enabled, "state must not change when the rule blocks the toggle");
    }

    [Fact]
    public void Constructor_InitiallyDisabled_HonorsFlag()
    {
        var dispatcher = new ActionDispatcher(BuildEngine(), initiallyEnabled: false);
        Assert.False(dispatcher.Enabled);
    }
}
