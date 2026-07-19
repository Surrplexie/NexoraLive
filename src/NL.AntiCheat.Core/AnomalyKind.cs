namespace NL.AntiCheat.Core;

/// <summary>What kind of anomaly Phase 5 detected. Each kind maps to a well-known
/// <c>GameEvent</c> name so streamers can write <c>.nle</c> rules against them
/// (nl.txt: "the more complex the NLEvent config, the better the anti-cheat").</summary>
public enum AnomalyKind
{
    /// <summary>A gameplay action while the player is known-dead (shoot/move after death,
    /// before a respawn) — the classic "impossible action".</summary>
    ImpossibleAction,

    /// <summary>Too many of the same event from one player inside a short window
    /// (e.g. fire-rate macros / chat flood).</summary>
    RateSpike,

    /// <summary>A one-step position jump so large it can't be legitimate locomotion
    /// (teleport / speed hack signal).</summary>
    Teleport,
}
