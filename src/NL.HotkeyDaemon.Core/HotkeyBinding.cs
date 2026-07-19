namespace NL.HotkeyDaemon.Core;

/// <summary>One resolved "press this combo -> fire this named action" mapping.</summary>
public sealed record HotkeyBinding(HotkeyCombo Combo, string Action);
