namespace NL.Core.Ast;

/// <summary>
/// Abstract base for condition expressions inside an <c>if</c> statement.
/// Either a <see cref="Condition"/> (simple comparison) or a
/// <see cref="CompoundCondition"/> (<c>and</c> / <c>or</c> of two sub-expressions).
/// </summary>
public abstract record ConditionExpr;
