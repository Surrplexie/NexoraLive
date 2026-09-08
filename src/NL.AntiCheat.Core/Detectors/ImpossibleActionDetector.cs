using NL.Server.Core;

namespace NL.AntiCheat.Core.Detectors;

/// <summary>
/// Tracks a coarse alive/dead state per player and flags actions that are impossible while
/// dead (shoot / move / useItem after <c>playerDeath</c>/<c>death</c>, before a
/// <c>respawn</c>/<c>playerJoin</c>). This is Phase 5's primary "impossible action" signal.
/// </summary>
public sealed class ImpossibleActionDetector : IAnomalyDetector
{
    private static readonly HashSet<string> DeathEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "playerDeath", "death",
    };

    private static readonly HashSet<string> AliveEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "playerJoin", "respawn",
    };

    private static readonly HashSet<string> ActionEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "shoot", "move", "useItem", "attack",
    };

    private readonly Dictionary<string, bool> _alive = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AnomalySignal> Observe(SessionEvent sessionEvent, DateTimeOffset nowUtc)
    {
        var player = sessionEvent.PlayerName;
        if (player is null)
        {
            return Array.Empty<AnomalySignal>();
        }

        var name = sessionEvent.Event.Name;

        if (DeathEvents.Contains(name))
        {
            _alive[player] = false;
            return Array.Empty<AnomalySignal>();
        }

        if (AliveEvents.Contains(name))
        {
            _alive[player] = true;
            return Array.Empty<AnomalySignal>();
        }

        // Explicit numeric alive flag from the game, if present.
        if (sessionEvent.Event.Properties.TryGetValue("player.alive", out var aliveProp))
        {
            _alive[player] = aliveProp > 0.5;
        }

        if (!ActionEvents.Contains(name))
        {
            return Array.Empty<AnomalySignal>();
        }

        if (_alive.TryGetValue(player, out var isAlive) && !isAlive)
        {
            return new[]
            {
                new AnomalySignal(
                    AnomalyKind.ImpossibleAction,
                    player,
                    name,
                    $"{name} while player is dead (impossible action).",
                    new Dictionary<string, double>
                    {
                        ["anomaly.playerAlive"] = 0,
                        ["anomaly.trigger"] = 1,
                    },
                    nowUtc),
            };
        }

        return Array.Empty<AnomalySignal>();
    }
}
