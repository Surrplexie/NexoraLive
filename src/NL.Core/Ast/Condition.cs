namespace NL.Core.Ast;

/// <summary>A single comparison: <c>operand COMPARATOR operand</c>.</summary>
public sealed record Condition(Operand Left, string Comparator, Operand Right) : ConditionExpr;
