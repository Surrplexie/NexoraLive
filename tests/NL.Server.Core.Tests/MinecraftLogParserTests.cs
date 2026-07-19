using NL.Server.Core;
using Xunit;

namespace NL.Server.Core.Tests;

public class MinecraftLogParserTests
{
    [Fact]
    public void Parse_JoinLine_ExtractsPlayerName()
    {
        var result = MinecraftLogParser.Parse("[21:14:05] [Server thread/INFO]: Steve joined the game");

        Assert.Equal(MinecraftEventKind.PlayerJoin, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
    }

    [Fact]
    public void Parse_LeaveLine_ExtractsPlayerName()
    {
        var result = MinecraftLogParser.Parse("[21:14:05] [Server thread/INFO]: Steve left the game");

        Assert.Equal(MinecraftEventKind.PlayerLeave, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
    }

    [Fact]
    public void Parse_ChatLine_ExtractsPlayerAndMessage()
    {
        var result = MinecraftLogParser.Parse("[21:14:10] [Server thread/INFO]: <Steve> hello world");

        Assert.Equal(MinecraftEventKind.PlayerChat, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
        Assert.Equal("hello world", result.ChatMessage);
    }

    [Fact]
    public void Parse_PaperNotSecureChat_ExtractsPlayerAndMessage()
    {
        var result = MinecraftLogParser.Parse(
            "[21:14:10] [Server thread/INFO]: [Not Secure] <Steve> hello paper");

        Assert.Equal(MinecraftEventKind.PlayerChat, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
        Assert.Equal("hello paper", result.ChatMessage);
    }

    [Fact]
    public void Parse_RespawnedLine_IsPlayerRespawn()
    {
        var result = MinecraftLogParser.Parse("[21:14:18] [Server thread/INFO]: Steve respawned");

        Assert.Equal(MinecraftEventKind.PlayerRespawn, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
    }

    [Theory]
    [InlineData("Steve was slain by Zombie", "slain")]
    [InlineData("Steve was shot by Skeleton", "shot")]
    [InlineData("Steve fell from a high place", "fell")]
    [InlineData("Steve fell out of the world", "fellOutOfWorld")]
    [InlineData("Steve drowned", "drowned")]
    [InlineData("Steve burned to death", "burned")]
    [InlineData("Steve starved to death", "starved")]
    [InlineData("Steve blew up", "blewUp")]
    [InlineData("Steve was blown up by Creeper", "blownUpBy")]
    [InlineData("Steve tried to swim in lava", "lava")]
    [InlineData("Steve withered away", "withered")]
    [InlineData("Steve was fireballed by Blaze", "fireballed")]
    [InlineData("Steve was pricked to death", "cactus")]
    [InlineData("Steve died", "died")]
    [InlineData("Steve froze to death", "froze")]
    [InlineData("Steve was frozen to death", "powderSnow")]
    [InlineData("Steve was obliterated by a sonically-charged shriek", "warden")]
    public void Parse_DeathLine_ExtractsPlayerAndCause(string content, string expectedCause)
    {
        var result = MinecraftLogParser.Parse($"[21:14:15] [Server thread/INFO]: {content}");

        Assert.Equal(MinecraftEventKind.PlayerDeath, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
        Assert.Equal(expectedCause, result.DeathCause);
    }

    [Fact]
    public void Parse_AdvancementLine_ExtractsPlayerAndTitle()
    {
        var result = MinecraftLogParser.Parse(
            "[21:14:20] [Server thread/INFO]: Steve has made the advancement [Stone Age]");

        Assert.Equal(MinecraftEventKind.PlayerAdvancement, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
        Assert.Equal("Stone Age", result.AdvancementTitle);
    }

    [Fact]
    public void Parse_ServerStartedLine_IsRecognized()
    {
        var result = MinecraftLogParser.Parse(
            "[21:13:50] [Server thread/INFO]: Done (23.456s)! For help, type \"help\"");

        Assert.Equal(MinecraftEventKind.ServerStarted, result.Kind);
    }

    [Fact]
    public void Parse_UnrecognizedLine_ReturnsUnknown()
    {
        var result = MinecraftLogParser.Parse("[21:14:00] [Server thread/INFO]: Saving the game (this may take a moment!)");

        Assert.Equal(MinecraftEventKind.Unknown, result.Kind);
    }

    [Fact]
    public void Parse_LineWithoutLogPrefix_StillWorks()
    {
        var result = MinecraftLogParser.Parse("Steve joined the game");

        Assert.Equal(MinecraftEventKind.PlayerJoin, result.Kind);
        Assert.Equal("Steve", result.PlayerName);
    }

    [Fact]
    public void Parse_ChatLineNotMistakenForJoinOrDeath()
    {
        // A player could type "left the game" in chat — must not be misparsed as a real leave.
        var result = MinecraftLogParser.Parse("[21:14:10] [Server thread/INFO]: <Steve> left the game");

        Assert.Equal(MinecraftEventKind.PlayerChat, result.Kind);
        Assert.Equal("left the game", result.ChatMessage);
    }
}
