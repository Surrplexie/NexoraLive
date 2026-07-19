using NL.Core;
using NL.Core.Ast;
using Xunit;

namespace NL.Core.Tests;

public class ParserTests
{
    [Fact]
    public void Parse_SimpleBlockAction_ProducesOneEventWithOneStatement()
    {
        var ast = Parser.Parse("event shoot:\n    block\n");

        var evt = Assert.Single(ast.Events);
        Assert.Equal("shoot", evt.Name);
        var action = Assert.IsType<ActionStatement>(Assert.Single(evt.Body));
        Assert.Equal(ActionKind.Block, action.Kind);
    }

    [Fact]
    public void Parse_IfElse_ProducesConditionAndBothBranches()
    {
        var source = "event respawn:\n    if player.health > 0:\n        block\n    else:\n        allow\n";
        var ast = Parser.Parse(source);

        var evt = Assert.Single(ast.Events);
        var ifStmt = Assert.IsType<IfStatement>(Assert.Single(evt.Body));
        var simple = Assert.IsType<Condition>(ifStmt.Condition);
        Assert.Equal("player.health", simple.Left.Text);
        Assert.Equal(">", simple.Comparator);
        Assert.Equal("0", simple.Right.Text);
        Assert.NotNull(ifStmt.Else);
        Assert.IsType<ActionStatement>(Assert.Single(ifStmt.Then));
        Assert.IsType<ActionStatement>(Assert.Single(ifStmt.Else!));
    }

    [Fact]
    public void Parse_WarnThenBlock_ProducesTwoStatementsInOrder()
    {
        var source = "event leaveBoundary:\n    warn \"stay within the zone\"\n    block\n";
        var ast = Parser.Parse(source);

        var evt = Assert.Single(ast.Events);
        Assert.Equal(2, evt.Body.Count);
        var warn = Assert.IsType<WarnStatement>(evt.Body[0]);
        Assert.Equal("stay within the zone", warn.Message);
        Assert.IsType<ActionStatement>(evt.Body[1]);
    }

    [Fact]
    public void Parse_DuplicateEventNames_Throws()
    {
        var source = "event shoot:\n    block\nevent shoot:\n    allow\n";
        Assert.Throws<NlSyntaxException>(() => Parser.Parse(source));
    }

    [Fact]
    public void Parse_EventWithNoIndentedBody_Throws()
    {
        var source = "event shoot:\n";
        Assert.Throws<NlSyntaxException>(() => Parser.Parse(source));
    }

    [Fact]
    public void Parse_ActionMissingNewlineEquivalent_UnknownTokenInBlock_Throws()
    {
        // "warn" without a following string literal is a syntax error, not a silent no-op.
        var source = "event shoot:\n    warn\n";
        Assert.Throws<NlSyntaxException>(() => Parser.Parse(source));
    }

    // --- hotkey declaration tests ---

    [Fact]
    public void Parse_SingleHotkeyDeclaration_ProducesOneBinding()
    {
        var ast = Parser.Parse("hotkey \"Ctrl+Alt+0\": toggleMic\n");

        Assert.Single(ast.HotkeyDeclarations);
        Assert.Empty(ast.Events);
        Assert.Equal("Ctrl+Alt+0", ast.HotkeyDeclarations[0].ComboText);
        Assert.Equal("toggleMic", ast.HotkeyDeclarations[0].Action);
    }

    [Fact]
    public void Parse_MultipleHotkeyDeclarations_AllProduced()
    {
        var source = """
            hotkey "Ctrl+Alt+0": toggleMic
            hotkey "Ctrl+Alt+9": announce
            hotkey "Ctrl+Alt+8": toggleNlEvents
            """;

        var ast = Parser.Parse(source);

        Assert.Equal(3, ast.HotkeyDeclarations.Count);
        Assert.Equal("toggleMic", ast.HotkeyDeclarations[0].Action);
        Assert.Equal("announce", ast.HotkeyDeclarations[1].Action);
        Assert.Equal("toggleNlEvents", ast.HotkeyDeclarations[2].Action);
    }

    [Fact]
    public void Parse_HotkeyDeclarationsInterleavedWithEventBlocks_BothCollected()
    {
        var source = """
            hotkey "Ctrl+Alt+0": toggleMic

            event shoot:
                block

            hotkey "Ctrl+Alt+9": announce

            event respawn:
                allow
            """;

        var ast = Parser.Parse(source);

        Assert.Equal(2, ast.Events.Count);
        Assert.Equal(2, ast.HotkeyDeclarations.Count);
        Assert.Equal("shoot", ast.Events[0].Name);
        Assert.Equal("respawn", ast.Events[1].Name);
        Assert.Equal("toggleMic", ast.HotkeyDeclarations[0].Action);
        Assert.Equal("announce", ast.HotkeyDeclarations[1].Action);
    }

    [Fact]
    public void Parse_DuplicateHotkeyCombo_SecondIsDroppedWithWarning()
    {
        var source = """
            hotkey "Ctrl+Alt+0": toggleMic
            hotkey "Ctrl+Alt+0": announce
            """;

        var ast = Parser.Parse(source, out var warnings);

        Assert.Single(ast.HotkeyDeclarations);
        Assert.Equal("toggleMic", ast.HotkeyDeclarations[0].Action);
        Assert.Contains(warnings, w => w.Contains("Ctrl+Alt+0") && w.Contains("more than once"));
    }

    [Fact]
    public void Parse_DuplicateComboIsCaseInsensitive()
    {
        var source = """
            hotkey "ctrl+alt+0": toggleMic
            hotkey "Ctrl+Alt+0": announce
            """;

        var ast = Parser.Parse(source, out var warnings);

        Assert.Single(ast.HotkeyDeclarations);
        Assert.Single(warnings);
    }

    [Fact]
    public void Parse_HotkeyMissingComboString_Throws()
    {
        var source = "hotkey toggleMic\n";
        Assert.Throws<NlSyntaxException>(() => Parser.Parse(source));
    }

    [Fact]
    public void Parse_HotkeyMissingColon_Throws()
    {
        var source = "hotkey \"Ctrl+Alt+0\" toggleMic\n";
        Assert.Throws<NlSyntaxException>(() => Parser.Parse(source));
    }

    [Fact]
    public void Parse_FileWithOnlyHotkeyDeclarations_NoEvents_IsValid()
    {
        var source = "hotkey \"Ctrl+Alt+0\": toggleMic\n";
        var ast = Parser.Parse(source);

        Assert.Empty(ast.Events);
        Assert.Single(ast.HotkeyDeclarations);
    }

    // --- and / or compound condition tests ---

    [Fact]
    public void Parse_AndCondition_ProducesCompoundCondition()
    {
        var source = "event respawn:\n    if player.health > 0 and player.hasItem == 1:\n        block\n    else:\n        allow\n";
        var ast = Parser.Parse(source);

        var ifStmt = Assert.IsType<IfStatement>(Assert.Single(Assert.Single(ast.Events).Body));
        var compound = Assert.IsType<CompoundCondition>(ifStmt.Condition);
        Assert.Equal("and", compound.Op);
        Assert.IsType<Condition>(compound.Left);
        Assert.IsType<Condition>(compound.Right);
    }

    [Fact]
    public void Parse_OrCondition_ProducesCompoundCondition()
    {
        var source = "event shoot:\n    if player.health == 0 or player.hasItem == 0:\n        allow\n    else:\n        block\n";
        var ast = Parser.Parse(source);

        var ifStmt = Assert.IsType<IfStatement>(Assert.Single(Assert.Single(ast.Events).Body));
        var compound = Assert.IsType<CompoundCondition>(ifStmt.Condition);
        Assert.Equal("or", compound.Op);
    }

    [Fact]
    public void Parse_ChainedAndOr_IsLeftAssociative()
    {
        // A and B or C  →  (A and B) or C
        var source = "event shoot:\n    if player.health > 0 and player.hasItem == 1 or player.health == 50:\n        block\n    else:\n        allow\n";
        var ast = Parser.Parse(source);

        var ifStmt = Assert.IsType<IfStatement>(Assert.Single(Assert.Single(ast.Events).Body));
        var outer = Assert.IsType<CompoundCondition>(ifStmt.Condition);
        Assert.Equal("or", outer.Op);
        var inner = Assert.IsType<CompoundCondition>(outer.Left);
        Assert.Equal("and", inner.Op);
    }
}
