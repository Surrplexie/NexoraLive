namespace NL.Moderation.Core;

/// <summary>What kind of moderation record this is — an automatic rule-engine decision being
/// logged as an audit trail, or a human mod/admin action that changed a SP's standing/history
/// (nl.txt: "real-time tools for warnings, bans, analytics, and behavioral monitoring").</summary>
public enum ModerationActionKind
{
    /// <summary>A `RuleEngine`/`NlServerHost` decision (Allow/Block/Warn), logged verbatim — no
    /// SP standing change, just the audit trail ROADMAP.md Phase 4 asks for.</summary>
    AutomaticDecision,

    /// <summary>A mod/admin issued a warning — adds a <c>SpOffense</c>.</summary>
    Warning,

    /// <summary>A mod/admin banned the SP from this streamer — adds a <c>SpOffense</c> and
    /// sets standing to <c>Banned</c>.</summary>
    Ban,

    /// <summary>A mod/admin put the SP under investigation — sets standing to <c>Graylist</c>,
    /// no offense (may be a false accusation, per nl.txt).</summary>
    GraylistHold,

    /// <summary>A mod/admin cleared a SP back to <c>Normal</c> standing (e.g. investigation
    /// resolved in the SP's favor).</summary>
    StandingCleared,
}
