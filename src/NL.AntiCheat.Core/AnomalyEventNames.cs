namespace NL.AntiCheat.Core;

/// <summary>Well-known <c>GameEvent.Name</c> values produced by Phase 5. Streamers author
/// <c>.nle</c> blocks against these names the same way they author blocks for <c>shoot</c> or
/// <c>playerChat</c>.</summary>
public static class AnomalyEventNames
{
    public const string ImpossibleAction = "anomalyImpossibleAction";
    public const string RateSpike = "anomalyRateSpike";
    public const string Teleport = "anomalyTeleport";

    public static string For(AnomalyKind kind) => kind switch
    {
        AnomalyKind.ImpossibleAction => ImpossibleAction,
        AnomalyKind.RateSpike => RateSpike,
        AnomalyKind.Teleport => Teleport,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
