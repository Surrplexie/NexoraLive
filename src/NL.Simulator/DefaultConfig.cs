namespace NL.Simulator;

/// <summary>Used when the CLI is run with no config file argument, so it's runnable
/// out of the box. Mirrors the example in docs/NLEVENT_LANGUAGE_SPEC_v0.1.md.</summary>
public static class DefaultConfig
{
    public const string Source = """
        # No PvP shooting allowed during this event.
        event shoot:
            block

        # Respawning is fine if you're already dead; otherwise it's a rule-break.
        event respawn:
            if player.health > 0:
                block
            else:
                allow

        # Leaving the marked play area is discouraged but not run-ending.
        event leaveBoundary:
            warn "stay within the zone"
            block
        """;
}
