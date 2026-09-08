namespace NL.Core.Ast;

public sealed record IfStatement(ConditionExpr Condition, List<Statement> Then, List<Statement>? Else, int Line)
    : Statement(Line);
