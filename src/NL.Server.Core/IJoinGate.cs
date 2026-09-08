using NL.Core;
using NL.Core.Sp;

namespace NL.Server.Core;

/// <summary>
/// Optional join-eligibility check run by <see cref="NlServerHost"/> on
/// <c>playerJoin</c> events before the <see cref="RuleEngine"/>. Deny/Hold become
/// <see cref="Decision.Block"/> (kick via the action sink); Allow falls through to normal
/// rule evaluation.
/// </summary>
public interface IJoinGate
{
    /// <summary>Returns null when this event is not gated (not a join, or no player name).</summary>
    JoinGateOutcome? TryEvaluate(SessionEvent sessionEvent);
}

/// <summary>Result of a join-gate evaluation, ready for the host loop and audit log.</summary>
public sealed record JoinGateOutcome(
    ActionResult ActionResult,
    JoinResult JoinResult,
    string PlayerId);
