namespace NL.HotkeyDaemon.Core;

/// <summary>
/// A parsed, canonical form of a hotkey string like <c>"Ctrl+Alt+0"</c>. Deliberately has no
/// dependency on System.Windows.Forms/Win32 so it can be unit-tested cross-platform; the
/// Windows-only <c>NL.HotkeyDaemon</c> project maps <see cref="Key"/> to an actual virtual-key
/// code when it registers the real global hotkey.
/// </summary>
public sealed record HotkeyCombo(HotkeyModifiers Modifiers, string Key)
{
    private static readonly Dictionary<string, HotkeyModifiers> ModifierAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = HotkeyModifiers.Control,
        ["control"] = HotkeyModifiers.Control,
        ["alt"] = HotkeyModifiers.Alt,
        ["shift"] = HotkeyModifiers.Shift,
        ["win"] = HotkeyModifiers.Windows,
        ["windows"] = HotkeyModifiers.Windows,
        ["meta"] = HotkeyModifiers.Windows,
    };

    /// <summary>Parses combo strings such as "Ctrl+Alt+0", "Shift+F1", or "Win+Alt+K".
    /// Order and casing of the parts don't matter; at least one modifier is required so the
    /// hotkey is unlikely to collide with normal typing.</summary>
    public static HotkeyCombo Parse(string text)
    {
        if (!TryParse(text, out var combo, out var error))
        {
            throw new FormatException(error);
        }

        return combo!;
    }

    public static bool TryParse(string text, out HotkeyCombo? combo) => TryParse(text, out combo, out _);

    public static bool TryParse(string text, out HotkeyCombo? combo, out string? error)
    {
        combo = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "hotkey combo cannot be empty";
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = $"'{text}' needs at least one modifier and one key, e.g. 'Ctrl+Alt+0'";
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!ModifierAliases.TryGetValue(parts[i], out var modifier))
            {
                error = $"'{parts[i]}' in '{text}' is not a recognized modifier (ctrl/alt/shift/win)";
                return false;
            }

            modifiers |= modifier;
        }

        var key = parts[^1].ToUpperInvariant();
        if (ModifierAliases.ContainsKey(key))
        {
            error = $"'{text}' has no actual key after its modifiers";
            return false;
        }

        combo = new HotkeyCombo(modifiers, key);
        error = null;
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}
