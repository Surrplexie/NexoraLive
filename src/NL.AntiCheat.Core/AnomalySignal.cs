namespace NL.AntiCheat.Core;

/// <summary>One anomaly observation, before it is mapped to a <c>GameEvent</c> for the
/// <c>RuleEngine</c>. Pure data — no I/O.</summary>
public sealed record AnomalySignal(
    AnomalyKind Kind,
    string? PlayerName,
    string TriggerEvent,
    string Reason,
    IReadOnlyDictionary<string, double> Metrics,
    DateTimeOffset TimestampUtc);
