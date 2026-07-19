namespace NL.Core.Ast;

/// <summary>
/// The root of a parsed .nle file: zero or more <see cref="HotkeyDeclaration"/>s (the
/// <c>hotkey "combo": action</c> top-level form) plus one rule block per event name.
/// Both may be interleaved freely; ordering within each list reflects source order.
/// </summary>
public sealed record ConfigAst(List<EventBlock> Events, List<HotkeyDeclaration> HotkeyDeclarations)
{
    public ConfigAst(List<EventBlock> events) : this(events, new List<HotkeyDeclaration>()) { }
}
