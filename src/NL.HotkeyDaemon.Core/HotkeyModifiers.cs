namespace NL.HotkeyDaemon.Core;

/// <summary>
/// Mirrors the Win32 <c>MOD_*</c> constants used by <c>RegisterHotKey</c>, kept here (rather
/// than in the Windows-only project) so combo parsing/formatting stays unit-testable on any OS.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}
