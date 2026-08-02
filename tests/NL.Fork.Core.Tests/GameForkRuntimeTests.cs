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
    [InlineData("hello-fork", "nl-fork-hello:latest", ForkGameKind.Hello)]
    public void Resolve_MapsGameIds(string gameId, string image, ForkGameKind kind)
    {
        var profile = ForkGameProfiles.Resolve(gameId);
        Assert.Equal(kind, profile.Game);
        Assert.Equal(image, profile.DockerImage);
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
