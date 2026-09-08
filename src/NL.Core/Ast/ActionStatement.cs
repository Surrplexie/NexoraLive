namespace NL.Core.Ast;

public sealed record ActionStatement(ActionKind Kind, int Line) : Statement(Line);
