using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Generic;
using Xunit;

namespace NL.Server.Core.Tests;

public class GenericJsonLineParserTests
{
    [Fact]
    public void TryParse_FullLine_BuildsSessionEvent()
    {
        var session = GenericJsonLineParser.TryParse(
            """{"event":"shoot","player":"Alice","props":{"weapon.damage":12.5,"flags.crit":true}}""");

        Assert.NotNull(session);
        Assert.Equal("shoot", session!.Event.Name);
        Assert.Equal("Alice", session.PlayerName);
        Assert.Equal(12.5, session.Event.Properties["weapon.damage"]);
        Assert.Equal(1.0, session.Event.Properties["flags.crit"]);
    }

    [Fact]
    public void TryParse_EventOnly_AllowsMissingPlayerAndProps()
    {
        var session = GenericJsonLineParser.TryParse("""{"event":"tick"}""");

        Assert.Equal("tick", session!.Event.Name);
        Assert.Null(session.PlayerName);
        Assert.Empty(session.Event.Properties);
    }

    [Fact]
    public void TryParse_HashComment_ReturnsNull()
    {
        Assert.Null(GenericJsonLineParser.TryParse("# not json"));
    }

    [Fact]
    public void TryParse_BlankLine_ReturnsNull()
    {
        Assert.Null(GenericJsonLineParser.TryParse("   "));
    }

    [Fact]
    public void TryParse_MissingEvent_Throws()
    {
        Assert.Throws<FormatException>(() => GenericJsonLineParser.TryParse("""{"player":"Alice"}"""));
    }

    [Fact]
    public void TryParse_NonNumericProp_Throws()
    {
        Assert.Throws<FormatException>(() =>
            GenericJsonLineParser.TryParse("""{"event":"x","props":{"label":"sword"}}"""));
    }
}

public class NlServerHostTests
{
    [Fact]
    public async Task RunAsync_EvaluatesAndAppliesBlocksOnly()
    {
        const string config = """
            event shoot:
                block

            event wave:
                allow
            """;

        var source = new ArrayEventSource(new[]
        {
            new SessionEvent(GameEvent.Simple("shoot"), "Alice"),
            new SessionEvent(GameEvent.Simple("wave"), "Bob"),
            new SessionEvent(GameEvent.Simple("shoot"), "Carol"),
        });

        var sink = new RecordingActionSink();
        var host = new NlServerHost(RuleEngine.FromSource(config), source, sink);

        await host.RunAsync(CancellationToken.None);

        Assert.Equal(3, host.Decisions.Count);
        Assert.Equal(2, sink.Applied.Count);
        Assert.All(sink.Applied, a => Assert.Equal(Decision.Block, a.Result.Decision));
        Assert.Equal("Alice", sink.Applied[0].Session.PlayerName);
        Assert.Equal("Carol", sink.Applied[1].Session.PlayerName);
    }
}
