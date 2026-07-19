using NL.Core.Ast;

namespace NL.Core;

/// <summary>
/// Recursive-descent parser that turns a <see cref="Token"/> stream (from <see cref="Lexer"/>)
/// into a <see cref="ConfigAst"/>. Mirrors the grammar in docs/NLEVENT_LANGUAGE_SPEC_v0.1.md.
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    private Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public static ConfigAst Parse(string source) => Parse(source, out _);

    public static ConfigAst Parse(string source, out IReadOnlyList<string> parseWarnings)
    {
        var tokens = Lexer.Tokenize(source);
        var parser = new Parser(tokens);
        var ast = parser.ParseConfig();
        parseWarnings = parser.ParseWarnings;
        return ast;
    }

    private Token Current => _tokens[_pos];

    private Token Advance() => _tokens[_pos++];

    private Token Expect(TokenType type, string what)
    {
        if (Current.Type != type)
        {
            throw new NlSyntaxException($"expected {what}, got {Current.Type} '{Current.Text}'", Current.Line);
        }

        return Advance();
    }

    private readonly List<string> _parseWarnings = new();

    /// <summary>Non-fatal warnings produced while parsing (e.g. duplicate hotkey combos).
    /// Embedded in <see cref="ConfigAst"/> so callers can surface them without failing.</summary>
    public IReadOnlyList<string> ParseWarnings => _parseWarnings;

    private ConfigAst ParseConfig()
    {
        var events = new List<EventBlock>();
        var hotkeys = new List<HotkeyDeclaration>();
        var seenEvents = new HashSet<string>();
        var seenCombos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (Current.Type != TokenType.Eof)
        {
            if (Current.Type == TokenType.Hotkey)
            {
                var decl = ParseHotkeyDeclaration();
                if (!seenCombos.Add(decl.ComboText))
                {
                    _parseWarnings.Add($"line {decl.Line}: hotkey combo '{decl.ComboText}' is declared more than once; only the first binding is used");
                }
                else
                {
                    hotkeys.Add(decl);
                }
            }
            else
            {
                var block = ParseEventBlock();
                if (!seenEvents.Add(block.Name))
                {
                    throw new NlSyntaxException($"duplicate 'event {block.Name}' block", block.Line);
                }

                events.Add(block);
            }
        }

        return new ConfigAst(events, hotkeys);
    }

    private HotkeyDeclaration ParseHotkeyDeclaration()
    {
        var tok = Expect(TokenType.Hotkey, "'hotkey'");
        var comboTok = Expect(TokenType.String, "a quoted combo string like \"Ctrl+Alt+0\"");
        Expect(TokenType.Colon, "':'");
        var actionTok = Expect(TokenType.Identifier, "an action name");
        Expect(TokenType.Newline, "a newline after the action name");
        return new HotkeyDeclaration(comboTok.Text, actionTok.Text, tok.Line);
    }

    private EventBlock ParseEventBlock()
    {
        var eventTok = Expect(TokenType.Event, "'event'");
        var nameTok = Expect(TokenType.Identifier, "an event name");
        Expect(TokenType.Colon, "':'");
        Expect(TokenType.Newline, "a newline after ':'");
        var body = ParseBlockBody();
        return new EventBlock(nameTok.Text, body, eventTok.Line);
    }

    private List<Statement> ParseBlockBody()
    {
        Expect(TokenType.Indent, "an indented block");
        var statements = new List<Statement>();

        while (Current.Type != TokenType.Dedent && Current.Type != TokenType.Eof)
        {
            statements.Add(ParseStatement());
        }

        Expect(TokenType.Dedent, "end of block (dedent)");

        if (statements.Count == 0)
        {
            throw new NlSyntaxException("a block cannot be empty", Current.Line);
        }

        return statements;
    }

    private Statement ParseStatement()
    {
        return Current.Type switch
        {
            TokenType.Block or TokenType.Allow or TokenType.Deny => ParseActionStatement(),
            TokenType.Warn => ParseWarnStatement(),
            TokenType.If => ParseIfStatement(),
            _ => throw new NlSyntaxException(
                $"unexpected token {Current.Type} '{Current.Text}' inside a block", Current.Line),
        };
    }

    private Statement ParseActionStatement()
    {
        var tok = Advance();
        var kind = tok.Type switch
        {
            TokenType.Block => ActionKind.Block,
            TokenType.Allow => ActionKind.Allow,
            TokenType.Deny => ActionKind.Deny,
            _ => throw new NlSyntaxException("expected an action (block/allow/deny)", tok.Line),
        };

        Expect(TokenType.Newline, "a newline after the action");
        return new ActionStatement(kind, tok.Line);
    }

    private Statement ParseWarnStatement()
    {
        var tok = Expect(TokenType.Warn, "'warn'");
        var message = Expect(TokenType.String, "a quoted warning message");
        Expect(TokenType.Newline, "a newline after the warn message");
        return new WarnStatement(message.Text, tok.Line);
    }

    private Statement ParseIfStatement()
    {
        var tok = Expect(TokenType.If, "'if'");
        var condition = ParseConditionExpr();
        Expect(TokenType.Colon, "':'");
        Expect(TokenType.Newline, "a newline after ':'");
        var thenBody = ParseBlockBody();

        List<Statement>? elseBody = null;
        if (Current.Type == TokenType.Else)
        {
            Advance();
            Expect(TokenType.Colon, "':'");
            Expect(TokenType.Newline, "a newline after ':'");
            elseBody = ParseBlockBody();
        }

        return new IfStatement(condition, thenBody, elseBody, tok.Line);
    }

    /// <summary>
    /// conditionExpr := simpleCondition { ("and" | "or") simpleCondition }
    /// Left-associative; no precedence difference between "and" and "or" in v0.1.
    /// </summary>
    private ConditionExpr ParseConditionExpr()
    {
        ConditionExpr expr = ParseSimpleCondition();

        while (Current.Type is TokenType.And or TokenType.Or)
        {
            var opTok = Advance();
            var right = ParseSimpleCondition();
            expr = new CompoundCondition(expr, opTok.Text, right);
        }

        return expr;
    }

    private Condition ParseSimpleCondition()
    {
        var left = ParseOperand();
        var opTok = Expect(TokenType.Comparator, "a comparator (>, <, >=, <=, ==, !=)");
        var right = ParseOperand();
        return new Condition(left, opTok.Text, right);
    }

    private Operand ParseOperand()
    {
        var tok = Current;
        var kind = tok.Type switch
        {
            TokenType.Identifier => OperandKind.Identifier,
            TokenType.Number => OperandKind.Number,
            TokenType.String => OperandKind.String,
            _ => throw new NlSyntaxException($"expected a value, got {tok.Type} '{tok.Text}'", tok.Line),
        };

        Advance();
        return new Operand(kind, tok.Text);
    }
}
