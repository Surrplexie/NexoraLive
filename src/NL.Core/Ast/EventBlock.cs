namespace NL.Core.Ast;

public sealed record EventBlock(string Name, List<Statement> Body, int Line);
