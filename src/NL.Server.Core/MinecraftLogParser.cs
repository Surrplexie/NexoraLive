using System.Text.RegularExpressions;

namespace NL.Server.Core;

/// <summary>
/// Parses lines from a vanilla Minecraft Java server's console log (`logs/latest.log`, or the
/// live stdout stream) into <see cref="ParsedMinecraftEvent"/>s. Pure text-in/record-out, no
/// file or network I/O, so it's fully unit-testable without a running server — see
/// <c>tests/NL.Server.Core.Tests/MinecraftLogParserTests.cs</c>.
///
/// This intentionally covers a curated, common subset of vanilla log lines (join/leave/chat/
/// advancement/server-started, plus the most frequent death message templates) rather than
/// every message vanilla or modded servers can produce — see docs/NLSERVER_MINECRAFT.md for
/// the full caveat and how to extend it.
/// </summary>
public static class MinecraftLogParser
{
    // Vanilla log4j line shape: "[21:14:05] [Server thread/INFO]: <content>"
    private static readonly Regex LogPrefix = new(
        @"^\[\d{2}:\d{2}:\d{2}\]\s*\[[^\]]*\]:\s*(?<content>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex Joined = new(@"^(?<player>[A-Za-z0-9_]{1,16}) joined the game$", RegexOptions.Compiled);
    private static readonly Regex Left = new(@"^(?<player>[A-Za-z0-9_]{1,16}) left the game$", RegexOptions.Compiled);
    private static readonly Regex Chat = new(@"^<(?<player>[A-Za-z0-9_]{1,16})> (?<message>.*)$", RegexOptions.Compiled);
    // Paper 1.19+ secure-chat prefix
    private static readonly Regex ChatNotSecure = new(
        @"^\[Not Secure\] <(?<player>[A-Za-z0-9_]{1,16})> (?<message>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex Respawned = new(
        @"^(?<player>[A-Za-z0-9_]{1,16}) respawned$",
        RegexOptions.Compiled);
    private static readonly Regex ServerStarted = new(@"^Done \([\d.]+s\)!", RegexOptions.Compiled);

    private static readonly Regex Advancement = new(
        @"^(?<player>[A-Za-z0-9_]{1,16}) has (?:made the advancement|reached the goal|completed the challenge) \[(?<title>.+)\]$",
        RegexOptions.Compiled);

    // A curated subset of vanilla's death message templates (grouped by shared "verb phrase").
    // Full vanilla list has ~50 templates plus mod-added ones; extend this array as needed.
    private static readonly (Regex Pattern, string Cause)[] DeathTemplates =
    [
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was slain by (?<killer>.+)$"), "slain"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was shot by (?<killer>.+)$"), "shot"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was killed by (?<killer>.+)$"), "killed"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was blown up by (?<killer>.+)$"), "blownUpBy"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) blew up$"), "blewUp"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) fell from a high place$"), "fell"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) fell out of the world$"), "fellOutOfWorld"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) drowned$"), "drowned"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) burned to death$"), "burned"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) went up in flames$"), "burned"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) walked into fire whilst fighting (?<killer>.+)$"), "burned"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) tried to swim in lava$"), "lava"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was fireballed by (?<killer>.+)$"), "fireballed"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was pummeled by (?<killer>.+)$"), "pummeled"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was squashed by (?<killer>.+)$"), "squashed"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was pricked to death$"), "cactus"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) hit the ground too hard$"), "fell"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was doomed to fall(?: by (?<killer>.+))?$"), "fell"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) starved to death$"), "starved"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) withered away$"), "withered"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was impaled by (?<killer>.+)$"), "impaled"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) experienced kinetic energy$"), "elytra"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) died$"), "died"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was killed trying to hurt (?<killer>.+)$"), "thorns"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was poked to death by a sweet berry bush$"), "berryBush"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was stung to death$"), "bee"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) froze to death$"), "froze"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was frozen to death(?: by (?<killer>.+))?$"), "powderSnow"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was skewered by a falling stalactite$"), "stalactite"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was squashed by a falling anvil(?: by (?<killer>.+))?$"), "anvil"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was roasted in dragon breath(?: by (?<killer>.+))?$"), "dragonBreath"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) went off with a bang(?: whilst fighting (?<killer>.+))?$"), "firework"),
        (new Regex(@"^(?<player>[A-Za-z0-9_]{1,16}) was obliterated by a sonically-charged shriek(?: whilst trying to escape (?<killer>.+))?$"), "warden"),
    ];

    /// <summary>
    /// Attempts to parse one raw log line (with or without the "[HH:mm:ss] [thread/LEVEL]: "
    /// prefix) into a <see cref="ParsedMinecraftEvent"/>. Returns an <see
    /// cref="MinecraftEventKind.Unknown"/> event (never null/throws) for lines that don't match
    /// any known template, so callers can safely ignore/log the rest of the console chatter.
    /// </summary>
    public static ParsedMinecraftEvent Parse(string rawLine)
    {
        var content = StripLogPrefix(rawLine).Trim();
        if (content.Length == 0)
        {
            return new ParsedMinecraftEvent(MinecraftEventKind.Unknown);
        }

        if (ServerStarted.IsMatch(content))
        {
            return new ParsedMinecraftEvent(MinecraftEventKind.ServerStarted);
        }

        var chatMatch = ChatNotSecure.Match(content);
        if (!chatMatch.Success)
        {
            chatMatch = Chat.Match(content);
        }

        if (chatMatch.Success)
        {
            return new ParsedMinecraftEvent(
                MinecraftEventKind.PlayerChat,
                PlayerName: chatMatch.Groups["player"].Value,
                ChatMessage: chatMatch.Groups["message"].Value);
        }

        var joinedMatch = Joined.Match(content);
        if (joinedMatch.Success)
        {
            return new ParsedMinecraftEvent(MinecraftEventKind.PlayerJoin, PlayerName: joinedMatch.Groups["player"].Value);
        }

        var leftMatch = Left.Match(content);
        if (leftMatch.Success)
        {
            return new ParsedMinecraftEvent(MinecraftEventKind.PlayerLeave, PlayerName: leftMatch.Groups["player"].Value);
        }

        var respawnMatch = Respawned.Match(content);
        if (respawnMatch.Success)
        {
            return new ParsedMinecraftEvent(MinecraftEventKind.PlayerRespawn, PlayerName: respawnMatch.Groups["player"].Value);
        }

        var advancementMatch = Advancement.Match(content);
        if (advancementMatch.Success)
        {
            return new ParsedMinecraftEvent(
                MinecraftEventKind.PlayerAdvancement,
                PlayerName: advancementMatch.Groups["player"].Value,
                AdvancementTitle: advancementMatch.Groups["title"].Value);
        }

        foreach (var (pattern, cause) in DeathTemplates)
        {
            var match = pattern.Match(content);
            if (match.Success)
            {
                return new ParsedMinecraftEvent(
                    MinecraftEventKind.PlayerDeath,
                    PlayerName: match.Groups["player"].Value,
                    DeathCause: cause);
            }
        }

        return new ParsedMinecraftEvent(MinecraftEventKind.Unknown);
    }

    private static string StripLogPrefix(string rawLine)
    {
        var match = LogPrefix.Match(rawLine);
        return match.Success ? match.Groups["content"].Value : rawLine;
    }
}
