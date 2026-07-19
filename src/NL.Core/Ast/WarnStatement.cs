namespace NL.Core.Ast;

/// <summary>Attaches <see cref="Message"/> to whichever action statement decides next
/// in the same statement list (see language spec for the exact fallthrough rules).</summary>
public sealed record WarnStatement(string Message, int Line) : Statement(Line);
