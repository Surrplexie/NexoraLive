using System.Drawing;
using System.Windows.Forms;
using NL.HotkeyDaemon.Core;

namespace NL.HotkeyDaemon;

/// <summary>
/// An invisible <see cref="Form"/> that exists purely to own a real Win32 window handle:
/// <c>RegisterHotKey</c> needs an HWND, and <c>WM_HOTKEY</c> messages get delivered to that
/// HWND's message loop regardless of which application currently has focus (so this keeps
/// working while a game is focused, not just while our own window is).
/// </summary>
internal sealed class HotkeyWindow : Form
{
    private readonly Dictionary<int, HotkeyBinding> _bindingsById = new();
    private int _nextId = 1;

    public HotkeyWindow()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        Opacity = 0;
        Size = new Size(0, 0);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2000, -2000);

        // Force the underlying HWND to exist immediately, without ever calling Show().
        _ = Handle;
    }

    public event Action<HotkeyBinding>? HotkeyPressed;

    public bool TryRegister(HotkeyBinding binding, out string? error)
    {
        if (!NativeMethods.TryGetVirtualKeyCode(binding.Combo.Key, out var vk))
        {
            error = $"'{binding.Combo.Key}' is not a recognized key";
            return false;
        }

        var id = _nextId++;
        var win32Modifiers = NativeMethods.ToWin32Modifiers(binding.Combo.Modifiers);

        if (!NativeMethods.RegisterHotKey(Handle, id, win32Modifiers, vk))
        {
            error = $"Windows refused to register {binding.Combo} (it may already be bound by another app)";
            return false;
        }

        _bindingsById[id] = binding;
        error = null;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _bindingsById.Keys)
        {
            NativeMethods.UnregisterHotKey(Handle, id);
        }

        _bindingsById.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && _bindingsById.TryGetValue(m.WParam.ToInt32(), out var binding))
        {
            HotkeyPressed?.Invoke(binding);
        }

        base.WndProc(ref m);
    }

    protected override void SetVisibleCore(bool value)
    {
        // Never actually show this window on screen, even if something tries to.
        base.SetVisibleCore(false);
    }
}
