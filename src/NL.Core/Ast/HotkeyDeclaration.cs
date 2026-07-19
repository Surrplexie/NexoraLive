namespace NL.Core.Ast;

/// <summary>
/// A top-level <c>hotkey "Ctrl+Alt+0": toggleMic</c> declaration in a .nle file. The combo
/// string is stored as raw text here — validation against known modifier/key names happens
/// in NL.HotkeyDaemon (which owns <c>HotkeyCombo</c>) rather than in NL.Core, keeping
/// this library free of any Windows/daemon-specific dependency.
/// </summary>
public sealed record HotkeyDeclaration(string ComboText, string Action, int Line);
