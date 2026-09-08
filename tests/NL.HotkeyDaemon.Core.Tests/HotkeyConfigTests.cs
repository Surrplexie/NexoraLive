using NL.HotkeyDaemon.Core;
using Xunit;

namespace NL.HotkeyDaemon.Core.Tests;

public class HotkeyConfigTests
{
    [Fact]
    public void Parse_ValidBindings_ProducesNoWarnings()
    {
        const string json = """
            {
              "bindings": [
                { "combo": "Ctrl+Alt+0", "action": "toggleMic" },
                { "combo": "Ctrl+Alt+9", "action": "announce" }
              ]
            }
            """;

        var config = HotkeyConfig.Parse(json);

        Assert.Equal(2, config.Bindings.Count);
        Assert.Empty(config.Warnings);
        Assert.Equal("toggleMic", config.Bindings[0].Action);
        Assert.Equal(HotkeyCombo.Parse("Ctrl+Alt+0"), config.Bindings[0].Combo);
    }

    [Fact]
    public void Parse_InvalidCombo_IsSkippedWithWarningAndDoesNotThrow()
    {
        const string json = """
            {
              "bindings": [
                { "combo": "NotAHotkey", "action": "toggleMic" },
                { "combo": "Ctrl+Alt+9", "action": "announce" }
              ]
            }
            """;

        var config = HotkeyConfig.Parse(json);

        Assert.Single(config.Bindings);
        Assert.Equal("announce", config.Bindings[0].Action);
        Assert.Contains(config.Warnings, w => w.Contains("NotAHotkey"));
    }

    [Fact]
    public void Parse_MissingAction_IsSkippedWithWarning()
    {
        const string json = """
            {
              "bindings": [
                { "combo": "Ctrl+Alt+0" }
              ]
            }
            """;

        var config = HotkeyConfig.Parse(json);

        Assert.Empty(config.Bindings);
        Assert.Single(config.Warnings);
    }

    [Fact]
    public void Parse_DuplicateCombo_KeepsOnlyFirstAndWarns()
    {
        const string json = """
            {
              "bindings": [
                { "combo": "Ctrl+Alt+0", "action": "toggleMic" },
                { "combo": "ctrl+alt+0", "action": "announce" }
              ]
            }
            """;

        var config = HotkeyConfig.Parse(json);

        Assert.Single(config.Bindings);
        Assert.Equal("toggleMic", config.Bindings[0].Action);
        Assert.Contains(config.Warnings, w => w.Contains("bound more than once"));
    }

    [Fact]
    public void Parse_EmptyBindingsArray_ProducesNoBindingsOrWarnings()
    {
        const string json = """{ "bindings": [] }""";

        var config = HotkeyConfig.Parse(json);

        Assert.Empty(config.Bindings);
        Assert.Empty(config.Warnings);
    }

    [Fact]
    public void Parse_MissingBindingsKey_ProducesEmptyConfig()
    {
        const string json = "{}";

        var config = HotkeyConfig.Parse(json);

        Assert.Empty(config.Bindings);
        Assert.Empty(config.Warnings);
    }
}
