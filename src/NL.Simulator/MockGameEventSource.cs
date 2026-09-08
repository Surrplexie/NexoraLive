using NL.Core;

namespace NL.Simulator;

/// <summary>A single fake in-game moment, with a human-readable description for CLI output.</summary>
public sealed record ScriptedEvent(string Description, GameEvent Event);

/// <summary>
/// Stands in for a real game's event stream (see ROADMAP.md Phase 3 for real integration).
/// Produces a small, fixed sequence of events that exercise every construct in the v0.1
/// language: a plain action, a conditional action, and a warn-then-block.
/// </summary>
public static class MockGameEventSource
{
    public static IEnumerable<ScriptedEvent> DefaultScript()
    {
        yield return new ScriptedEvent(
            "PlayerA fires a weapon",
            GameEvent.Simple("shoot"));

        yield return new ScriptedEvent(
            "PlayerB tries to respawn while still alive (health 40)",
            GameEvent.Create("respawn", ("player.health", 40)));

        yield return new ScriptedEvent(
            "PlayerC respawns normally after dying (health 0)",
            GameEvent.Create("respawn", ("player.health", 0)));

        yield return new ScriptedEvent(
            "PlayerD wanders outside the marked play zone",
            GameEvent.Simple("leaveBoundary"));

        yield return new ScriptedEvent(
            "PlayerE uses an item (no rule authored for this event)",
            GameEvent.Simple("useItem"));
    }
}
