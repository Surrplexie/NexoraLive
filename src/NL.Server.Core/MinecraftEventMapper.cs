using NL.Core;

namespace NL.Server.Core;

/// <summary>
/// Turns a <see cref="ParsedMinecraftEvent"/> into one or more <see cref="SessionEvent"/>s for
/// the shared NLServer pipeline. Tracks per-player alive/dead so Phase 5's impossible-action
/// detector can see <c>player.alive</c>, and emits a synthetic <c>respawn</c> when a dead
/// player next chats/advances (vanilla logs rarely print an explicit respawn line).
/// </summary>
public sealed class MinecraftEventMapper
{
    private readonly PlayerSessionTracker _tracker;
    private readonly HashSet<string> _dead = new(StringComparer.OrdinalIgnoreCase);

    public MinecraftEventMapper(PlayerSessionTracker? tracker = null)
    {
        _tracker = tracker ?? new PlayerSessionTracker();
    }

    /// <summary>Returns null when there is no meaningful mapping (Unknown / ServerStarted).</summary>
    public SessionEvent? Map(ParsedMinecraftEvent parsed)
    {
        var all = MapAll(parsed);
        return all.Count == 0 ? null : all[^1];
    }

    /// <summary>Full mapping; may include a synthetic <c>respawn</c> before the primary event.</summary>
    public IReadOnlyList<SessionEvent> MapAll(ParsedMinecraftEvent parsed)
    {
        switch (parsed.Kind)
        {
            case MinecraftEventKind.PlayerJoin:
            {
                var name = parsed.PlayerName!;
                _dead.Remove(name);
                var stats = _tracker.RecordJoin(name);
                return new[]
                {
                    new SessionEvent(
                        GameEvent.Create(
                            "playerJoin",
                            ("player.sessionJoinCount", stats.JoinCount),
                            ("player.alive", 1)),
                        name),
                };
            }

            case MinecraftEventKind.PlayerLeave:
            {
                var name = parsed.PlayerName!;
                _dead.Remove(name);
                return new[]
                {
                    new SessionEvent(
                        GameEvent.Create("playerLeave", ("player.alive", 0)),
                        name),
                };
            }

            case MinecraftEventKind.PlayerChat:
            {
                var name = parsed.PlayerName!;
                var message = parsed.ChatMessage ?? "";
                var letters = message.Where(char.IsLetter).ToArray();
                var capsRatio = letters.Length == 0 ? 0.0 : letters.Count(char.IsUpper) / (double)letters.Length;
                var isCommand = message.StartsWith('/') ? 1.0 : 0.0;
                var events = new List<SessionEvent>();
                MaybeAddRespawn(events, name);
                events.Add(new SessionEvent(
                    GameEvent.Create(
                        "playerChat",
                        ("chat.length", message.Length),
                        ("chat.capsRatio", capsRatio),
                        ("chat.isCommand", isCommand),
                        ("player.alive", 1)),
                    name));
                return events;
            }

            case MinecraftEventKind.PlayerDeath:
            {
                var name = parsed.PlayerName!;
                _dead.Add(name);
                var stats = _tracker.RecordDeath(name);
                return new[]
                {
                    new SessionEvent(
                        GameEvent.Create(
                            "playerDeath",
                            ("player.sessionDeathCount", stats.DeathCount),
                            ("player.alive", 0)),
                        name),
                };
            }

            case MinecraftEventKind.PlayerRespawn:
            {
                var name = parsed.PlayerName!;
                _dead.Remove(name);
                return new[]
                {
                    new SessionEvent(
                        GameEvent.Create("respawn", ("player.health", 0), ("player.alive", 1)),
                        name),
                };
            }

            case MinecraftEventKind.PlayerAdvancement:
            {
                var name = parsed.PlayerName!;
                var stats = _tracker.RecordAdvancement(name);
                var events = new List<SessionEvent>();
                MaybeAddRespawn(events, name);
                events.Add(new SessionEvent(
                    GameEvent.Create(
                        "playerAdvancement",
                        ("player.sessionAdvancementCount", stats.AdvancementCount),
                        ("player.alive", 1)),
                    name));
                return events;
            }

            case MinecraftEventKind.ServerStarted:
            case MinecraftEventKind.Unknown:
            default:
                return Array.Empty<SessionEvent>();
        }
    }

    private void MaybeAddRespawn(List<SessionEvent> events, string playerName)
    {
        if (!_dead.Remove(playerName))
        {
            return;
        }

        events.Add(new SessionEvent(
            GameEvent.Create("respawn", ("player.health", 0), ("player.alive", 1)),
            playerName));
    }
}
