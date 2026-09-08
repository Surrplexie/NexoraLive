namespace NL.Core.Sp;

/// <summary>What <see cref="JoinEligibilityEngine"/> decided for one join attempt, mirroring
/// the shape of Phase 0's <see cref="ActionResult"/> so callers/tests feel consistent.</summary>
public sealed record JoinResult(JoinDecision Decision, string? Reason)
{
    public static JoinResult Allow(string? reason = null) => new(JoinDecision.Allow, reason);

    public static JoinResult Deny(string reason) => new(JoinDecision.Deny, reason);

    public static JoinResult Hold(string reason) => new(JoinDecision.Hold, reason);

    public override string ToString() => Reason is null ? Decision.ToString() : $"{Decision} ({Reason})";
}
