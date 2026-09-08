namespace NL.Core;

/// <summary>
/// All token kinds recognized by the NLEvent v0.1 lexer. See
/// docs/NLEVENT_LANGUAGE_SPEC_v0.1.md for the grammar these compose into.
/// </summary>
public enum TokenType
{
    Event,
    Hotkey,
    If,
    Else,
    And,
    Or,
    Block,
    Allow,
    Deny,
    Warn,
    Identifier,
    Number,
    String,
    Colon,
    Comparator,
    Newline,
    Indent,
    Dedent,
    Eof,
}

/// <summary>A single lexical token with its source line for error reporting.</summary>
public sealed record Token(TokenType Type, string Text, int Line)
{
    public override string ToString() => $"{Type}('{Text}') @ line {Line}";
}
