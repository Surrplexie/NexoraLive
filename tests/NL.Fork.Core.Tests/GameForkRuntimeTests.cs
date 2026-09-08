using NL.Core;
using NL.Fork.Core;
using Xunit;

namespace NL.Fork.Core.Tests;

public class ForkGameProfileTests
{
    [Theory]
    [InlineData("minecraft", "nl-fork-minecraft:latest", ForkGameKind.Minecraft)]
    [InlineData("minecraft-paper", "nl-fork-minecraft-paper:latest", ForkGameKind.Minecraft)]
    [InlineData("beamng", "nl-fork-beamng:latest", ForkGameKind.Beamng)]
    [InlineData("rimworld", "nl-fork-rimworld:latest", ForkGameKind.RimWorld)]
    [InlineData("kenshi", "nl-fork-kenshi:latest", ForkGameKind.Kenshi)]
    [InlineData("hello-fork", "nl-fork-hello:latest", ForkGameKind.Hello)]
    public void Resolve_MapsGameIds(string gameId, string image, ForkGameKind kind)
    {
        var profile = ForkGameProfiles.Resolve(gameId);
        Assert.Equal(kind, profile.Game);
        Assert.Equal(image, profile.DockerImage);
    }

    [Fact]
    public void Resolve_RimWorld_HasConnectSchemeAndPort()
    {
        var profile = ForkGameProfiles.Resolve("rimworld");
        Assert.Equal("rimworld", profile.ConnectScheme);
        Assert.Equal(25555, profile.PlayerConnectPort);
        Assert.Equal("configs/rimworld.nle", profile.DefaultNleTemplate);
    }

    [Fact]
    public void Resolve_Kenshi_HasConnectSchemeAndPort()
    {
        var profile = ForkGameProfiles.Resolve("kenshi");
        Assert.Equal("kenshi", profile.ConnectScheme);
        Assert.Equal(23386, profile.PlayerConnectPort);
        Assert.Equal("configs/kenshi.nle", profile.DefaultNleTemplate);
    }
}

public class MinecraftForkRuntimeTests
{
    private const string MinecraftNle = """
        event playerJoin:
            allow
        event playerChat:
            if chat.capsRatio > 0.8 and chat.length > 10:
                block
            else:
                allow
        event entityDamage:
            allow
        """;

    [Fact]
    public async Task TryChatAsync_CapsBlock_DoesNotCommit()
    {
        var session = new EmbeddedForkSession(MinecraftNle, game: ForkGameKind.Minecraft);
        await session.Runtime.TryJoinAsync("Steve");

        var chat = await session.Runtime.TryChatAsync("Steve", "HELLO EVERYONE!!!");
        Assert.False(chat.Committed);
        Assert.Equal(Decision.Block, chat.Decision);
    }

    [Fact]
    public async Task TryChatAsync_NormalMessage_Allows()
    {
        var session = new EmbeddedForkSession(MinecraftNle, game: ForkGameKind.Minecraft);
        await session.Runtime.TryJoinAsync("Steve");

        var chat = await session.Runtime.TryChatAsync("Steve", "hello team");
        Assert.True(chat.Committed);
    }
}

public class BeamngForkRuntimeTests
{
    private const string BeamngNle = """
        event sessionStart:
            allow
        event playerJoin:
            allow
        event move:
            if vehicle.speed > 55:
                block
            else:
                allow
        event crash:
            if crash.severity > 12:
                block
            else:
                allow
        """;

    [Fact]
    public async Task TryMoveAsync_SpeedLimit_BlocksOver55()
    {
        var session = new EmbeddedForkSession(BeamngNle, game: ForkGameKind.Beamng);
        await session.Runtime.TryJoinAsync("Driver1");

        var slow = await session.Runtime.TryMoveAsync("Driver1", 40, 0, 0);
        Assert.True(slow.Committed);

        var fast = await session.Runtime.TryMoveAsync("Driver1", 62, 0, 0);
        Assert.False(fast.Committed);
        Assert.Equal(Decision.Block, fast.Decision);
    }

    [Fact]
    public async Task TryCrashAsync_HighSeverity_Blocks()
    {
        var session = new EmbeddedForkSession(BeamngNle, game: ForkGameKind.Beamng);
        var beamng = (BeamngForkRuntime)session.Runtime;
        await beamng.TryJoinAsync("Driver1");

        var crash = await beamng.TryCrashAsync("Driver1", 14);
        Assert.False(crash.Committed);
        Assert.Equal(Decision.Block, crash.Decision);
    }
}

public class RimWorldForkRuntimeTests
{
    private const string RimWorldNle = """
        event sessionStart:
            allow
        event playerJoin:
            allow
        event playerChat:
            if chat.capsRatio > 0.8 and chat.length > 10:
                block
            else:
                allow
        event entityDamage:
            if weapon.damage > 40:
                block
            else:
                allow
        event buildingDestroy:
            if building.value > 1500:
                block
            else:
                allow
        event zoneEdit:
            if zone.tiles > 80:
                block
            else:
                allow
        event animalRelease:
            if animal.count > 3:
                block
            else:
                allow
        event colonistDraft:
            if player.downed > 0:
                block
            else:
                allow
        event trade:
            if trade.value > 5000:
                block
            else:
                allow
        event itemDestroy:
            if item.value > 800:
                block
            else:
                allow
        event colonistDown:
            allow
        event respawn:
            allow
        event move:
            allow
        """;

    [Fact]
    public async Task TryChatAsync_CapsBlock_DoesNotCommit()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        await session.Runtime.TryJoinAsync("Randy");

        var chat = await session.Runtime.TryChatAsync("Randy", "HELLO COLONY!!!");
        Assert.False(chat.Committed);
        Assert.Equal(Decision.Block, chat.Decision);
    }

    [Fact]
    public async Task TryBuildingDestroyAsync_HighValue_Blocks()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = (RimWorldForkRuntime)session.Runtime;
        await rim.TryJoinAsync("Randy");

        var ok = await rim.TryBuildingDestroyAsync("Randy", 100);
        Assert.True(ok.Committed);

        var grief = await rim.TryBuildingDestroyAsync("Randy", 2500);
        Assert.False(grief.Committed);
        Assert.Equal(Decision.Block, grief.Decision);
    }

    [Fact]
    public async Task TryAnimalReleaseAsync_MassRelease_Blocks()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = (RimWorldForkRuntime)session.Runtime;
        await rim.TryJoinAsync("Randy");

        var mass = await rim.TryAnimalReleaseAsync("Randy", 6);
        Assert.False(mass.Committed);
        Assert.Equal(Decision.Block, mass.Decision);
    }

    [Fact]
    public async Task TryZoneEditAsync_MassEdit_Blocks()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = (RimWorldForkRuntime)session.Runtime;
        await rim.TryJoinAsync("Randy");

        var ok = await rim.TryZoneEditAsync("Randy", 10);
        Assert.True(ok.Committed);
        var mass = await rim.TryZoneEditAsync("Randy", 120);
        Assert.False(mass.Committed);
        Assert.Equal(Decision.Block, mass.Decision);
    }

    [Fact]
    public async Task TryShootAsync_ExcessiveDamage_BlocksAndPreservesHealth()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        await session.Runtime.TryJoinAsync("Randy");
        await session.Runtime.TryJoinAsync("Cassandra");

        var blocked = await session.Runtime.TryShootAsync("Randy", "Cassandra", 50);
        Assert.False(blocked.Committed);
        Assert.True(session.Runtime.World.TryGetPlayer("Cassandra", out var cass));
        Assert.Equal(100, cass!.Health);
    }

    [Fact]
    public async Task TryShootAsync_LethalDamage_DownsColonist()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = (RimWorldForkRuntime)session.Runtime;
        await rim.TryJoinAsync("Randy");
        await rim.TryJoinAsync("Cassandra");

        Assert.True((await rim.TryShootAsync("Randy", "Cassandra", 40)).Committed);
        Assert.True((await rim.TryShootAsync("Randy", "Cassandra", 40)).Committed);
        Assert.True((await rim.TryShootAsync("Randy", "Cassandra", 30)).Committed);

        Assert.True(rim.World.TryGetPlayer("Cassandra", out var cass));
        Assert.True(cass!.Downed);
        Assert.False(cass.Alive);

        var rescue = await rim.TryRespawnAsync("Cassandra");
        Assert.True(rescue.Committed);
        Assert.False(cass.Downed);
        Assert.Equal(100, cass.Health);
    }

    [Fact]
    public async Task TryTradeAsync_HighValue_Blocks()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = (RimWorldForkRuntime)session.Runtime;
        await rim.TryJoinAsync("Randy");

        Assert.True((await rim.TryTradeAsync("Randy", 100)).Committed);
        var blocked = await rim.TryTradeAsync("Randy", 9000);
        Assert.False(blocked.Committed);
    }

    [Fact]
    public async Task TryItemDestroyAsync_HighValue_Blocks()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = (RimWorldForkRuntime)session.Runtime;
        await rim.TryJoinAsync("Randy");

        var blocked = await rim.TryItemDestroyAsync("Randy", 1200);
        Assert.False(blocked.Committed);
    }

    [Fact]
    public async Task TryDraftAsync_DownedColonist_Blocks()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = (RimWorldForkRuntime)session.Runtime;
        await rim.TryJoinAsync("Randy");
        await rim.TryColonistDownAsync("Randy");

        var draft = await rim.TryDraftAsync("Randy", true);
        Assert.False(draft.Committed);
        Assert.False(rim.World.Players["Randy"].Drafted);
    }

    [Fact]
    public async Task TryMoveAsync_CommitsWithinColonyBounds()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        await session.Runtime.TryJoinAsync("Randy");

        var move = await session.Runtime.TryMoveAsync("Randy", 12, 4, 0);
        Assert.True(move.Committed);
        Assert.Equal(12, session.Runtime.World.Players["Randy"].X);
    }

    [Fact]
    public async Task TryJoinAsync_AdmitDenied_DoesNotSpawn()
    {
        var session = new EmbeddedForkSession(RimWorldNle, game: ForkGameKind.RimWorld);
        var rim = new RimWorldForkRuntime(
            new RuleEngineForkDecisionSink(session.Engine),
            admitAsync: _ => Task.FromResult(false));
        var join = await rim.TryJoinAsync("Randy");
        Assert.False(join.Committed);
        Assert.Empty(rim.World.Players);
    }
}

public class KenshiForkRuntimeTests
{
    private const string KenshiNle = """
        event sessionStart:
            allow
        event playerJoin:
            allow
        event playerChat:
            if chat.capsRatio > 0.8 and chat.length > 10:
                block
            else:
                allow
        event entityDamage:
            if weapon.damage > 40:
                block
            else:
                allow
        event buildingDestroy:
            if building.value > 1500:
                block
            else:
                allow
        event steal:
            if steal.value > 800:
                block
            else:
                allow
        event raid:
            if raid.severity > 8:
                block
            else:
                allow
        event squadOrder:
            if player.downed > 0:
                block
            else:
                allow
        event trade:
            if trade.value > 5000:
                block
            else:
                allow
        event itemDestroy:
            if item.value > 800:
                block
            else:
                allow
        event knockdown:
            allow
        event respawn:
            allow
        event move:
            allow
        """;

    [Fact]
    public async Task TryChatAsync_CapsBlock_DoesNotCommit()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        await session.Runtime.TryJoinAsync("Beep");

        var chat = await session.Runtime.TryChatAsync("Beep", "HELLO SQUAD!!!");
        Assert.False(chat.Committed);
        Assert.Equal(Decision.Block, chat.Decision);
    }

    [Fact]
    public async Task TryStealAsync_HighValue_Blocks()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = (KenshiForkRuntime)session.Runtime;
        await ken.TryJoinAsync("Beep");

        var ok = await ken.TryStealAsync("Beep", 100);
        Assert.True(ok.Committed);

        var theft = await ken.TryStealAsync("Beep", 1200);
        Assert.False(theft.Committed);
        Assert.Equal(Decision.Block, theft.Decision);
    }

    [Fact]
    public async Task TryRaidAsync_HighSeverity_Blocks()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = (KenshiForkRuntime)session.Runtime;
        await ken.TryJoinAsync("Beep");

        var mass = await ken.TryRaidAsync("Beep", 12);
        Assert.False(mass.Committed);
        Assert.Equal(Decision.Block, mass.Decision);
    }

    [Fact]
    public async Task TryBuildingDestroyAsync_HighValue_Blocks()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = (KenshiForkRuntime)session.Runtime;
        await ken.TryJoinAsync("Beep");

        var grief = await ken.TryBuildingDestroyAsync("Beep", 2500);
        Assert.False(grief.Committed);
        Assert.Equal(Decision.Block, grief.Decision);
    }

    [Fact]
    public async Task TryShootAsync_ExcessiveDamage_BlocksAndPreservesHealth()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        await session.Runtime.TryJoinAsync("Beep");
        await session.Runtime.TryJoinAsync("Shinobi");

        var blocked = await session.Runtime.TryShootAsync("Beep", "Shinobi", 50);
        Assert.False(blocked.Committed);
        Assert.True(session.Runtime.World.TryGetPlayer("Shinobi", out var shinobi));
        Assert.Equal(100, shinobi!.Health);
    }

    [Fact]
    public async Task TryShootAsync_LethalDamage_KnockdownsCharacter()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = (KenshiForkRuntime)session.Runtime;
        await ken.TryJoinAsync("Beep");
        await ken.TryJoinAsync("Shinobi");

        Assert.True((await ken.TryShootAsync("Beep", "Shinobi", 40)).Committed);
        Assert.True((await ken.TryShootAsync("Beep", "Shinobi", 40)).Committed);
        Assert.True((await ken.TryShootAsync("Beep", "Shinobi", 30)).Committed);

        Assert.True(ken.World.TryGetPlayer("Shinobi", out var shinobi));
        Assert.True(shinobi!.Downed);
        Assert.False(shinobi.Alive);

        var rescue = await ken.TryRespawnAsync("Shinobi");
        Assert.True(rescue.Committed);
        Assert.False(shinobi.Downed);
        Assert.Equal(100, shinobi.Health);
    }

    [Fact]
    public async Task TryTradeAsync_HighValue_Blocks()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = (KenshiForkRuntime)session.Runtime;
        await ken.TryJoinAsync("Beep");

        Assert.True((await ken.TryTradeAsync("Beep", 100)).Committed);
        var blocked = await ken.TryTradeAsync("Beep", 9000);
        Assert.False(blocked.Committed);
    }

    [Fact]
    public async Task TryItemDestroyAsync_HighValue_Blocks()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = (KenshiForkRuntime)session.Runtime;
        await ken.TryJoinAsync("Beep");

        var blocked = await ken.TryItemDestroyAsync("Beep", 1200);
        Assert.False(blocked.Committed);
    }

    [Fact]
    public async Task TrySquadOrderAsync_KnockedOut_Blocks()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = (KenshiForkRuntime)session.Runtime;
        await ken.TryJoinAsync("Beep");
        await ken.TryKnockdownAsync("Beep");

        var order = await ken.TrySquadOrderAsync("Beep", true);
        Assert.False(order.Committed);
        Assert.False(ken.World.Players["Beep"].Drafted);
    }

    [Fact]
    public async Task TryMoveAsync_CommitsWithinBounds()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        await session.Runtime.TryJoinAsync("Beep");

        var move = await session.Runtime.TryMoveAsync("Beep", 12, 4, 0);
        Assert.True(move.Committed);
        Assert.Equal(12, session.Runtime.World.Players["Beep"].X);
    }

    [Fact]
    public async Task TryJoinAsync_AdmitDenied_DoesNotSpawn()
    {
        var session = new EmbeddedForkSession(KenshiNle, game: ForkGameKind.Kenshi);
        var ken = new KenshiForkRuntime(
            new RuleEngineForkDecisionSink(session.Engine),
            admitAsync: _ => Task.FromResult(false));
        var join = await ken.TryJoinAsync("Beep");
        Assert.False(join.Committed);
        Assert.Empty(ken.World.Players);
    }
}

