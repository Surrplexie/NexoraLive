using NL.Server.Core;
using Xunit;

namespace NL.Server.Core.Tests;

public class MinecraftEventMapperTests
{
    [Fact]
    public void Map_PlayerJoin_ProducesJoinEventWithSessionCount()
    {
        var mapper = new MinecraftEventMapper();
        var parsed = new ParsedMinecraftEvent(MinecraftEventKind.PlayerJoin, PlayerName: "Steve");

        var mapped = mapper.Map(parsed);

        Assert.NotNull(mapped);
        Assert.Equal("playerJoin", mapped!.Event.Name);
        Assert.Equal("Steve", mapped.PlayerName);
        Assert.Equal(1, mapped.Event.Properties["player.sessionJoinCount"]);
    }

    [Fact]
    public void Map_PlayerJoin_SecondJoinIncrementsSessionCount()
    {
        var mapper = new MinecraftEventMapper();
        mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerJoin, PlayerName: "Steve"));
        var second = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerJoin, PlayerName: "Steve"));

        Assert.Equal(2, second!.Event.Properties["player.sessionJoinCount"]);
    }

    [Fact]
    public void Map_PlayerLeave_ProducesLeaveEventWithAliveFlag()
    {
        var mapper = new MinecraftEventMapper();
        var mapped = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerLeave, PlayerName: "Steve"));

        Assert.Equal("playerLeave", mapped!.Event.Name);
        Assert.Equal(0, mapped.Event.Properties["player.alive"]);
    }

    [Fact]
    public void MapAll_ChatWhileDead_EmitsRespawnThenChat()
    {
        var mapper = new MinecraftEventMapper();
        mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerDeath, PlayerName: "Steve", DeathCause: "fell"));

        var events = mapper.MapAll(new ParsedMinecraftEvent(
            MinecraftEventKind.PlayerChat, PlayerName: "Steve", ChatMessage: "I'm back"));

        Assert.Equal(2, events.Count);
        Assert.Equal("respawn", events[0].Event.Name);
        Assert.Equal(1, events[0].Event.Properties["player.alive"]);
        Assert.Equal("playerChat", events[1].Event.Name);
    }

    [Fact]
    public void Map_PlayerDeath_SetsAliveZero()
    {
        var mapper = new MinecraftEventMapper();
        var mapped = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerDeath, PlayerName: "Steve", DeathCause: "fell"));

        Assert.Equal(0, mapped!.Event.Properties["player.alive"]);
    }

    [Theory]
    [InlineData("hello there", 0.0)]
    [InlineData("HELLO THERE", 1.0)]
    public void Map_PlayerChat_ComputesCapsRatio(string message, double expectedRatio)
    {
        var mapper = new MinecraftEventMapper();
        var parsed = new ParsedMinecraftEvent(MinecraftEventKind.PlayerChat, PlayerName: "Steve", ChatMessage: message);

        var mapped = mapper.Map(parsed);

        Assert.Equal("playerChat", mapped!.Event.Name);
        Assert.Equal(expectedRatio, mapped.Event.Properties["chat.capsRatio"], precision: 5);
        Assert.Equal(message.Length, mapped.Event.Properties["chat.length"]);
    }

    [Fact]
    public void Map_PlayerChat_SlashCommandSetsIsCommandFlag()
    {
        var mapper = new MinecraftEventMapper();
        var parsed = new ParsedMinecraftEvent(MinecraftEventKind.PlayerChat, PlayerName: "Steve", ChatMessage: "/help");

        var mapped = mapper.Map(parsed);

        Assert.Equal(1.0, mapped!.Event.Properties["chat.isCommand"]);
    }

    [Fact]
    public void Map_PlayerDeath_TracksRunningDeathCountPerPlayer()
    {
        var mapper = new MinecraftEventMapper();
        mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerDeath, PlayerName: "Steve", DeathCause: "fell"));
        var second = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerDeath, PlayerName: "Steve", DeathCause: "drowned"));

        Assert.Equal(2, second!.Event.Properties["player.sessionDeathCount"]);
    }

    [Fact]
    public void Map_PlayerAdvancement_TracksRunningAdvancementCount()
    {
        var mapper = new MinecraftEventMapper();
        var mapped = mapper.Map(new ParsedMinecraftEvent(
            MinecraftEventKind.PlayerAdvancement, PlayerName: "Steve", AdvancementTitle: "Stone Age"));

        Assert.Equal("playerAdvancement", mapped!.Event.Name);
        Assert.Equal(1, mapped.Event.Properties["player.sessionAdvancementCount"]);
    }

    [Fact]
    public void Map_ServerStarted_ReturnsNull()
    {
        var mapper = new MinecraftEventMapper();
        var mapped = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.ServerStarted));

        Assert.Null(mapped);
    }

    [Fact]
    public void Map_Unknown_ReturnsNull()
    {
        var mapper = new MinecraftEventMapper();
        var mapped = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.Unknown));

        Assert.Null(mapped);
    }

    [Fact]
    public void Map_DifferentPlayers_TrackSeparateSessionCounts()
    {
        var mapper = new MinecraftEventMapper();
        var steve = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerJoin, PlayerName: "Steve"));
        mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerJoin, PlayerName: "Alex"));
        var steveAgain = mapper.Map(new ParsedMinecraftEvent(MinecraftEventKind.PlayerJoin, PlayerName: "Steve"));

        Assert.Equal(1, steve!.Event.Properties["player.sessionJoinCount"]);
        Assert.Equal(2, steveAgain!.Event.Properties["player.sessionJoinCount"]);
    }
}
