using NL.Core;
using NL.Server.Core;

namespace NL.AntiCheat.Core;

/// <summary>Maps an <see cref="AnomalySignal"/> into a <see cref="SessionEvent"/> so the
/// Phase 0 <see cref="RuleEngine"/> can evaluate it with zero changes — same "name + numeric
/// properties" contract as every other game event.</summary>
public static class AnomalyEventMapper
{
    public static SessionEvent ToSessionEvent(AnomalySignal signal)
    {
        var props = new Dictionary<string, double>(signal.Metrics)
        {
            ["anomaly.kind"] = (double)(int)signal.Kind,
            ["anomaly.severity"] = SeverityFor(signal.Kind),
        };

        return new SessionEvent(
            new GameEvent(AnomalyEventNames.For(signal.Kind), props),
            signal.PlayerName,
            signal.TimestampUtc);
    }

    private static double SeverityFor(AnomalyKind kind) => kind switch
    {
        AnomalyKind.ImpossibleAction => 2,
        AnomalyKind.Teleport => 2,
        AnomalyKind.RateSpike => 1,
        _ => 1,
    };
}
