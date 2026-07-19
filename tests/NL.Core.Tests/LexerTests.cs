using NL.Core;
using Xunit;

namespace NL.Core.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenize_SimpleEventBlock_ProducesExpectedSequence()
    {
        var tokens = Lexer.Tokenize("event shoot:\n    block\n");

        var types = tokens.Select(t => t.Type).ToArray();
        Assert.Equal(new[]
        {
            TokenType.Event, TokenType.Identifier, TokenType.Colon, TokenType.Newline,
            TokenType.Indent, TokenType.Block, TokenType.Newline,
            TokenType.Dedent, TokenType.Eof,
        }, types);
    }

    [Fact]
    public void Tokenize_CommentsAndBlankLines_AreIgnored()
    {
        var source = "# a comment\n\nevent shoot: # inline comment\n    block\n";
        var tokens = Lexer.Tokenize(source);

        Assert.DoesNotContain(tokens, t => t.Text.Contains("comment"));
        Assert.Equal(TokenType.Event, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_TabIndentation_Throws()
    {
        var source = "event shoot:\n\tblock\n";
        Assert.Throws<NlSyntaxException>(() => Lexer.Tokenize(source));
    }

    [Fact]
    public void Tokenize_UnterminatedString_Throws()
    {
        var source = "event leaveBoundary:\n    warn \"oops\n    block\n";
        Assert.Throws<NlSyntaxException>(() => Lexer.Tokenize(source));
    }

    [Fact]
    public void Tokenize_DottedIdentifier_IsSingleToken()
    {
        var source = "event respawn:\n    if player.health > 0:\n        block\n    else:\n        allow\n";
        var tokens = Lexer.Tokenize(source);

        var dotted = tokens.Single(t => t.Type == TokenType.Identifier && t.Text.Contains('.'));
        Assert.Equal("player.health", dotted.Text);
    }

    [Fact]
    public void Tokenize_InconsistentDedent_Throws()
    {
        // Dedents to a level (2 spaces) that was never pushed on the indent stack.
        var source = "event shoot:\n      block\n  allow\n";
        Assert.Throws<NlSyntaxException>(() => Lexer.Tokenize(source));
    }
}
