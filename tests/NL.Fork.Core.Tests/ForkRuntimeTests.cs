using NL.Core;
using NL.Core.Sp;
using NL.Fork.Core;
using NL.Server.Core;
using NL.Server.Core.Integration;
using Xunit;

namespace NL.Fork.Core.Tests;

public class ForkStateValidatorTests
{
    private readonly ForkStateValidator _validator = new();

    [Fact]
    public void ValidateShoot_RejectsDeadShooter()
    {
        var shooter = new ForkPlayerState { Name = "A", Health = 0 };
        var result = _validator.ValidateShoot(shooter, null, 10);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void ValidateMove_RejectsTeleport()
    {
        var world = new ForkWorldState();
        var player = world.AddPlayer("A");
        player.X = 0;
        var result = _validator.ValidateMove(player, 200, 0, 0, world);
        Assert.False(result.Allowed);
        Assert.Contains("teleport", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}

public class ForkModLoaderTests
{
    [Fact]
    public void ApplyMods_MergesPropOverrides()
    {
        var manifest = ForkModLoader.LoadFromJson("""
            { "mods": [ { "id": "x", "props": { "weapon.damage": 99 } } ] }
            """);

        var merged = ForkModLoader.ApplyMods(manifest, new Dictionary<string, double> { ["weapon.damage"] = 10 });
        Assert.Equal(99, merged["weapon.damage"]);
    }
}

public class NlActionEnvelopeParseTests
{
    [Fact]
    public void TryParse_RoundTripsBlockAction()
    {
        var session = new SessionEvent(new GameEvent("shoot", new Dictionary<string, double>()), "Alice");
        var line = NlActionEnvelope.Serialize(session, ActionResult.Block("no guns"));
        var parsed = NlActionEnvelope.TryParse(line);
        Assert.NotNull(parsed);
        Assert.Equal("Alice", parsed!.Player);
        Assert.Equal("shoot", parsed.Event);
        Assert.Equal("Block", parsed.Decision);
    }
}

public class HelloForkRuntimeTests
{
    private const string BlockShootNle = """
        event shoot:
            block
        event playerJoin:
            allow
        """;

    [Fact]
    public async Task TryShootAsync_BlockRule_DoesNotApplyDamage()
    {
        var session = new EmbeddedForkSession(BlockShootNle);
        await session.Runtime.TryJoinAsync("Alice");
        await session.Runtime.TryJoinAsync("Bob");

        var result = await session.Runtime.TryShootAsync("Alice", "Bob", 25);
        Assert.False(result.Committed);
        Assert.Equal(Decision.Block, result.Decision);

        Assert.True(session.Runtime.World.TryGetPlayer("Bob", out var bob));
        Assert.Equal(100, bob!.Health);
    }

    [Fact]
    public async Task TryShootAsync_AllowRule_AppliesDamage()
    {
        var session = new EmbeddedForkSession("""
            event shoot:
                allow
            event playerJoin:
                allow
            """);

        await session.Runtime.TryJoinAsync("Alice");
        await session.Runtime.TryJoinAsync("Bob");

        var result = await session.Runtime.TryShootAsync("Alice", "Bob", 25);
        Assert.True(result.Committed);
        Assert.True(session.Runtime.World.TryGetPlayer("Bob", out var bob));
        Assert.Equal(75, bob!.Health);
    }

    [Fact]
    public async Task TryJoinAsync_JoinGateBlock_PreventsSpawn()
    {
        var profiles = new Dictionary<string, SpProfile>();
        var joinGate = new SpJoinGate(
            "streamer-1",
            new JoinRequirements { RequireFollow = true },
            (id, name) =>
            {
                if (!profiles.TryGetValue(id, out var profile))
                {
                    profile = new SpProfile
                    {
                        Id = id,
                        DisplayName = name,
                        AccountCreatedAtUtc = DateTimeOffset.UtcNow,
                    };
                    profiles[id] = profile;
                }

                return profile;
            });

        var session = new EmbeddedForkSession(BlockShootNle, joinGate: joinGate);
        var join = await session.Runtime.TryJoinAsync("Stranger");
        Assert.False(join.Committed);
        Assert.False(session.Runtime.World.TryGetPlayer("Stranger", out _));
    }

    [Fact]
    public async Task TryMoveAsync_ImpossibleTeleport_RejectedLocally()
    {
        var session = new EmbeddedForkSession(BlockShootNle);
        await session.Runtime.TryJoinAsync("Alice");
        var move = await session.Runtime.TryMoveAsync("Alice", 500, 0, 0);
        Assert.False(move.Committed);
    }
}

public class ForkActionApplicatorTests
{
    [Fact]
    public void Apply_Kick_RemovesPlayer()
    {
        var world = new ForkWorldState();
        world.AddPlayer("Alice");
        var applicator = new ForkActionApplicator();
        applicator.Apply(new NlActionMessage("kick", "Alice", "playerJoin", "Block", "", 0), world);
        Assert.False(world.TryGetPlayer("Alice", out _));
    }
}
