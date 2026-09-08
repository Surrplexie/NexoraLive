using NL.Core;

namespace NL.Moderation.Core;

/// <summary>
/// One entry in the moderation audit log — either an automatic `RuleEngine` decision or a
/// human mod/admin action. ROADMAP.md Phase 4: "Persist ActionResults + who/what triggered
/// them as a log."
/// </summary>
public sealed record ModerationRecord(
    Guid Id,
    DateTimeOffset TimestampUtc,
    string StreamerId,
    ModerationActionKind Kind,
    string? PlayerId,
    string? PlayerName,
    string EventName,
    Decision? Decision,
    string? Message,
    string Source,
    string? IssuedBy)
{
    /// <summary>Logs an automatic decision from `RuleEngine`/`NlServerHost` — no SP id is
    /// known at this layer (Phase 3 only carries a display name), so <see cref="PlayerId"/>
    /// is null unless the caller resolved one.</summary>
    public static ModerationRecord ForAutomaticDecision(
        string streamerId,
        string? playerName,
        string eventName,
        Decision decision,
        string? message,
        string source,
        DateTimeOffset nowUtc,
        string? playerId = null) =>
        new(
            Guid.NewGuid(), nowUtc, streamerId, ModerationActionKind.AutomaticDecision,
            playerId, playerName, eventName, decision, message, source, IssuedBy: null);

    /// <summary>Logs a human mod/admin action (warning/ban/graylist/clear).</summary>
    public static ModerationRecord ForModAction(
        string streamerId,
        ModerationActionKind kind,
        string playerId,
        string? playerName,
        string reason,
        string issuedBy,
        DateTimeOffset nowUtc) =>
        new(
            Guid.NewGuid(), nowUtc, streamerId, kind,
            playerId, playerName, EventName: "manual", Decision: null,
            Message: reason, Source: "manual", IssuedBy: issuedBy);
}
