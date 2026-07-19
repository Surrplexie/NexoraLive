namespace NL.Core.Ast;

/// <summary>
/// Two <see cref="ConditionExpr"/>s joined by <c>and</c> or <c>or</c>.
/// Evaluation is short-circuit: <c>and</c> stops on first false, <c>or</c> stops on first true.
/// Multiple joins are left-associative:
/// <c>A and B or C</c> → <c>(A and B) or C</c>.
/// </summary>
public sealed record CompoundCondition(ConditionExpr Left, string Op, ConditionExpr Right)
    : ConditionExpr;
