namespace NL.Core.Ast;

/// <summary>One side of a <see cref="Condition"/>: a literal number/string, or an identifier
/// (e.g. "player.health") resolved against a GameEvent's properties at evaluation time.</summary>
public sealed record Operand(OperandKind Kind, string Text);
