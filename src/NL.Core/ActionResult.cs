namespace NL.Core;

public enum Decision
{
    Allow,
    Block,
}

/// <summary>What the <see cref="RuleEngine"/> decided for a given <see cref="GameEvent"/>.</summary>
public sealed record ActionResult(Decision Decision, string? Message)
{
    public static ActionResult Allow(string? message = null) => new(Decision.Allow, message);

    public static ActionResult Block(string? message = null) => new(Decision.Block, message);

    public override string ToString() => Message is null ? Decision.ToString() : $"{Decision} ({Message})";
}
