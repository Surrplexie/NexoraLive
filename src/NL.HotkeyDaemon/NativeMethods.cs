using System.Runtime.InteropServices;
using NL.HotkeyDaemon.Core;

namespace NL.HotkeyDaemon;

/// <summary>Thin P/Invoke wrapper around the Win32 global-hotkey APIs, plus the mapping from
/// a <see cref="HotkeyCombo"/>'s modifier/key strings to the raw values RegisterHotKey needs.</summary>
internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Brings a window to the foreground and activates it.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Shows/restores/minimizes a window (use <c>SW_RESTORE = 9</c> to un-minimize).</summary>
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public const int SW_RESTORE = 9;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    // Stops Windows from auto-repeating the hotkey message while the keys are held down.
    private const uint ModNoRepeat = 0x4000;

    private static readonly Dictionary<string, uint> NamedKeyCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SPACE"] = 0x20,
        ["ENTER"] = 0x0D,
        ["ESC"] = 0x1B,
        ["ESCAPE"] = 0x1B,
        ["TAB"] = 0x09,
        ["UP"] = 0x26,
        ["DOWN"] = 0x28,
        ["LEFT"] = 0x25,
        ["RIGHT"] = 0x27,
        ["INSERT"] = 0x2D,
        ["DELETE"] = 0x2E,
        ["HOME"] = 0x24,
        ["END"] = 0x23,
        ["PAGEUP"] = 0x21,
        ["PAGEDOWN"] = 0x22,
    };

    public static uint ToWin32Modifiers(HotkeyModifiers modifiers)
    {
        var result = ModNoRepeat;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= ModAlt;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= ModControl;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= ModShift;
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) result |= ModWin;
        return result;
    }

    /// <summary>Maps a canonical <see cref="HotkeyCombo.Key"/> ("0".."9", "A".."Z", "F1".."F24",
    /// or a handful of named keys) to a Win32 virtual-key code.</summary>
    public static bool TryGetVirtualKeyCode(string key, out uint virtualKeyCode)
    {
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is (>= '0' and <= '9') or (>= 'A' and <= 'Z'))
            {
                virtualKeyCode = c;
                return true;
            }
        }

        if (key.Length is 2 or 3 && (key[0] == 'F' || key[0] == 'f')
            && int.TryParse(key.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
        {
            virtualKeyCode = (uint)(0x70 + (fn - 1)); // VK_F1 == 0x70, sequential from there.
            return true;
        }

        if (NamedKeyCodes.TryGetValue(key, out var named))
        {
            virtualKeyCode = named;
            return true;
        }

        virtualKeyCode = 0;
        return false;
    }
}
