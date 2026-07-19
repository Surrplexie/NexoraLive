namespace NL.Core.Ast;

/// <summary>Base type for anything that can appear in an event block's body.</summary>
public abstract record Statement(int Line);
