using NL.Core;
using Xunit;

namespace NL.Core.Tests;

public class RuleEngineTests
{
    private const string SampleConfig = """
        event shoot:
            block

        event respawn:
            if player.health > 0:
                block
            else:
                allow

        event leaveBoundary:
            warn "stay within the zone"
            block
        """;

    [Fact]
    public void Evaluate_Shoot_IsBlockedWithNoMessage()
    {
        var engine = RuleEngine.FromSource(SampleConfig);
        var result = engine.Evaluate(GameEvent.Simple("shoot"));

        Assert.Equal(Decision.Block, result.Decision);
        Assert.Null(result.Message);
    }

    [Theory]
    [InlineData(40, Decision.Block)]
    [InlineData(0, Decision.Allow)]
    public void Evaluate_Respawn_DependsOnPlayerHealth(double health, Decision expected)
    {
        var engine = RuleEngine.FromSource(SampleConfig);
        var result = engine.Evaluate(GameEvent.Create("respawn", ("player.health", health)));

        Assert.Equal(expected, result.Decision);
    }

    [Fact]
    public void Evaluate_LeaveBoundary_IsBlockedWithWarningMessageAttached()
    {
        var engine = RuleEngine.FromSource(SampleConfig);
        var result = engine.Evaluate(GameEvent.Simple("leaveBoundary"));

        Assert.Equal(Decision.Block, result.Decision);
        Assert.Equal("stay within the zone", result.Message);
    }

    [Fact]
    public void Evaluate_EventWithNoAuthoredRule_DefaultsToAllow()
    {
        var engine = RuleEngine.FromSource(SampleConfig);
        var result = engine.Evaluate(GameEvent.Simple("useItem"));

        Assert.Equal(Decision.Allow, result.Decision);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Load_WellFormedConfig_HasNoLoadWarnings()
    {
        var engine = RuleEngine.FromSource(SampleConfig);
        Assert.Empty(engine.LoadWarnings);
    }

    [Fact]
    public void Load_IfWithoutElse_ProducesLoadWarningNamingTheEvent()
    {
        const string source = """
            event shoot:
                if player.health > 0:
                    block
            """;

        var engine = RuleEngine.FromSource(source);
        Assert.Contains(engine.LoadWarnings, w => w.Contains("shoot"));
    }

    [Fact]
    public void Evaluate_FallthroughWhenConditionFalseAndNoElse_DefaultsToAllow()
    {
        const string source = """
            event shoot:
                if player.health > 100:
                    block
            """;

        var engine = RuleEngine.FromSource(source);
        var result = engine.Evaluate(GameEvent.Create("shoot", ("player.health", 10)));

        Assert.Equal(Decision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_UnknownPropertyReference_Throws()
    {
        const string source = """
            event shoot:
                if player.mana > 0:
                    block
                else:
                    allow
            """;

        var engine = RuleEngine.FromSource(source);
        Assert.Throws<InvalidOperationException>(() => engine.Evaluate(GameEvent.Simple("shoot")));
    }

    // --- and / or evaluation tests ---

    [Theory]
    [InlineData(40, 1, Decision.Block)]   // both true  → and is true → block
    [InlineData(40, 0, Decision.Allow)]   // right false → and is false → else → allow
    [InlineData(0,  1, Decision.Allow)]   // left false  → and is false → else → allow
    [InlineData(0,  0, Decision.Allow)]   // both false  → and is false → else → allow
    public void Evaluate_AndCondition_BothMustBeTrue(double health, double hasItem, Decision expected)
    {
        const string source = """
            event respawn:
                if player.health > 0 and player.hasItem == 1:
                    block
                else:
                    allow
            """;

        var engine = RuleEngine.FromSource(source);
        var result = engine.Evaluate(GameEvent.Create("respawn",
            ("player.health", health), ("player.hasItem", hasItem)));

        Assert.Equal(expected, result.Decision);
    }

    [Theory]
    [InlineData(0,  0, Decision.Block)]   // both true  → or is true → block
    [InlineData(40, 0, Decision.Block)]   // right true → or is true → block
    [InlineData(0,  1, Decision.Block)]   // left true  → or is true → block
    [InlineData(40, 1, Decision.Allow)]   // both false → or is false → else → allow
    public void Evaluate_OrCondition_EitherSuffices(double health, double hasItem, Decision expected)
    {
        // block if dead (health == 0) OR not carrying item (hasItem == 0)
        const string source = """
            event shoot:
                if player.health == 0 or player.hasItem == 0:
                    block
                else:
                    allow
            """;

        var engine = RuleEngine.FromSource(source);
        var result = engine.Evaluate(GameEvent.Create("shoot",
            ("player.health", health), ("player.hasItem", hasItem)));

        Assert.Equal(expected, result.Decision);
    }

    [Fact]
    public void Evaluate_ChainedAndOrIsLeftAssociative_ShortCircuits()
    {
        // A and B or C — left assoc: (A and B) or C
        // health > 0 and hasItem == 1 or health == 99
        // For health=50, hasItem=0: (true and false) or false → false → else → allow
        // For health=99, hasItem=0: (true and false) or true → true → block
        const string source = """
            event shoot:
                if player.health > 0 and player.hasItem == 1 or player.health == 99:
                    block
                else:
                    allow
            """;

        var engine = RuleEngine.FromSource(source);

        var r1 = engine.Evaluate(GameEvent.Create("shoot", ("player.health", 50.0), ("player.hasItem", 0.0)));
        Assert.Equal(Decision.Allow, r1.Decision);

        var r2 = engine.Evaluate(GameEvent.Create("shoot", ("player.health", 99.0), ("player.hasItem", 0.0)));
        Assert.Equal(Decision.Block, r2.Decision);
    }
}
