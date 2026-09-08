namespace NL.Core.Ast;

/// <summary>The three terminal actions a rule can take, per NLEVENT_LANGUAGE_SPEC_v0.1.</summary>
public enum ActionKind
{
    Block,
    Allow,
    Deny,
}
