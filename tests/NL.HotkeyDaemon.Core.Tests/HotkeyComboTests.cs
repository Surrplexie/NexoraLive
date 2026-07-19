using NL.HotkeyDaemon.Core;
using Xunit;

namespace NL.HotkeyDaemon.Core.Tests;

public class HotkeyComboTests
{
    [Theory]
    [InlineData("Ctrl+Alt+0", HotkeyModifiers.Control | HotkeyModifiers.Alt, "0")]
    [InlineData("ctrl+alt+0", HotkeyModifiers.Control | HotkeyModifiers.Alt, "0")]
    [InlineData("Shift+F1", HotkeyModifiers.Shift, "F1")]
    [InlineData("Win+Alt+K", HotkeyModifiers.Windows | HotkeyModifiers.Alt, "K")]
    [InlineData("Control+Shift+Alt+Win+9", HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Windows, "9")]
    public void Parse_ValidCombos_ProducesExpectedModifiersAndKey(string text, HotkeyModifiers expectedModifiers, string expectedKey)
    {
        var combo = HotkeyCombo.Parse(text);

        Assert.Equal(expectedModifiers, combo.Modifiers);
        Assert.Equal(expectedKey, combo.Key);
    }

    [Fact]
    public void Parse_KeyIsCaseNormalizedToUpper()
    {
        var combo = HotkeyCombo.Parse("ctrl+alt+k");
        Assert.Equal("K", combo.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Bogus+0")]
    [InlineData("Ctrl+Alt")]
    public void TryParse_InvalidCombos_ReturnsFalse(string text)
    {
        var ok = HotkeyCombo.TryParse(text, out var combo);

        Assert.False(ok);
        Assert.Null(combo);
    }

    [Fact]
    public void Parse_InvalidCombo_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => HotkeyCombo.Parse("NotAHotkey"));
    }

    [Fact]
    public void ToString_RendersCanonicalOrderRegardlessOfInputOrder()
    {
        var combo = HotkeyCombo.Parse("Alt+Ctrl+0");
        Assert.Equal("Ctrl+Alt+0", combo.ToString());
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = HotkeyCombo.Parse("Ctrl+Alt+0");
        var b = HotkeyCombo.Parse("alt+ctrl+0");
        Assert.Equal(a, b);
    }
}
