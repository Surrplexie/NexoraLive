using System.Text;

namespace NL.Core;

/// <summary>
/// Turns .nle source text into a flat token stream, including synthetic
/// <see cref="TokenType.Indent"/>/<see cref="TokenType.Dedent"/> tokens derived from
/// leading whitespace (Python-style block structure). See
/// docs/NLEVENT_LANGUAGE_SPEC_v0.1.md for the exact lexical rules.
/// </summary>
public static class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["event"] = TokenType.Event,
        ["hotkey"] = TokenType.Hotkey,
        ["if"] = TokenType.If,
        ["else"] = TokenType.Else,
        ["and"] = TokenType.And,
        ["or"] = TokenType.Or,
        ["block"] = TokenType.Block,
        ["allow"] = TokenType.Allow,
        ["deny"] = TokenType.Deny,
        ["warn"] = TokenType.Warn,
    };

    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var indentStack = new Stack<int>();
        indentStack.Push(0);

        var lines = source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var lastRealLine = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var rawLine = lines[i];

            var (indent, rest) = SplitIndent(rawLine, lineNo);
            var content = StripComment(rest);

            if (content.Trim().Length == 0)
            {
                // Blank or comment-only line: doesn't affect indentation, no tokens emitted.
                continue;
            }

            lastRealLine = lineNo;

            if (indent > indentStack.Peek())
            {
                indentStack.Push(indent);
                tokens.Add(new Token(TokenType.Indent, "", lineNo));
            }
            else
            {
                while (indent < indentStack.Peek())
                {
                    indentStack.Pop();
                    tokens.Add(new Token(TokenType.Dedent, "", lineNo));
                }

                if (indent != indentStack.Peek())
                {
                    throw new NlSyntaxException(
                        $"inconsistent indentation (got {indent} spaces, no matching block level)", lineNo);
                }
            }

            TokenizeLineContent(content, lineNo, tokens);
            tokens.Add(new Token(TokenType.Newline, "", lineNo));
        }

        while (indentStack.Peek() > 0)
        {
            indentStack.Pop();
            tokens.Add(new Token(TokenType.Dedent, "", lastRealLine));
        }

        tokens.Add(new Token(TokenType.Eof, "", lastRealLine));
        return tokens;
    }

    private static (int Indent, string Remainder) SplitIndent(string line, int lineNo)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            if (line[i] == '\t')
            {
                throw new NlSyntaxException("tabs are not allowed for indentation; use spaces", lineNo);
            }

            i++;
        }

        return (i, line[i..]);
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inString = !inString;
            }
            else if (c == '#' && !inString)
            {
                return line[..i];
            }
        }

        return line;
    }

    private static void TokenizeLineContent(string content, int lineNo, List<Token> tokens)
    {
        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];

            if (c == ' ')
            {
                i++;
                continue;
            }

            if (c == ':')
            {
                tokens.Add(new Token(TokenType.Colon, ":", lineNo));
                i++;
                continue;
            }

            if (c is '>' or '<' or '=' or '!')
            {
                var start = i;
                i++;
                if (i < content.Length && content[i] == '=')
                {
                    i++;
                }

                var op = content[start..i];
                if (op is not (">" or "<" or ">=" or "<=" or "==" or "!="))
                {
                    throw new NlSyntaxException($"unrecognized operator '{op}'", lineNo);
                }

                tokens.Add(new Token(TokenType.Comparator, op, lineNo));
                continue;
            }

            if (c == '"')
            {
                var start = i + 1;
                var end = content.IndexOf('"', start);
                if (end < 0)
                {
                    throw new NlSyntaxException("unterminated string literal", lineNo);
                }

                tokens.Add(new Token(TokenType.String, content[start..end], lineNo));
                i = end + 1;
                continue;
            }

            if (char.IsDigit(c))
            {
                var start = i;
                while (i < content.Length && (char.IsDigit(content[i]) || content[i] == '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenType.Number, content[start..i], lineNo));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < content.Length && (char.IsLetterOrDigit(content[i]) || content[i] is '_' or '.'))
                {
                    i++;
                }

                var word = content[start..i];
                tokens.Add(Keywords.TryGetValue(word, out var kw)
                    ? new Token(kw, word, lineNo)
                    : new Token(TokenType.Identifier, word, lineNo));
                continue;
            }

            throw new NlSyntaxException($"unexpected character '{c}'", lineNo);
        }
    }
}
