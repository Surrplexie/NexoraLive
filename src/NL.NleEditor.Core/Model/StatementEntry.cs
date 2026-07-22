namespace NL.NleEditor.Model;

public enum StatementType { Allow, Block, Deny, Warn, If }

public class StatementEntry
{
    public StatementType Type { get; set; } = StatementType.Allow;

    public string? WarnMessage { get; set; }

    public ConditionEntry? Condition { get; set; }
    public List<StatementEntry> ThenBody { get; set; } = [];
    public List<StatementEntry>? ElseBody { get; set; }

    public string ToDisplayString(int depth = 0)
    {
        var indent = new string(' ', depth * 2);
        return Type switch
        {
            StatementType.Allow => $"{indent}✓ allow",
            StatementType.Block => $"{indent}✗ block",
            StatementType.Deny  => $"{indent}✗ deny",
            StatementType.Warn  => $"{indent}⚠ warn \"{WarnMessage}\"",
            StatementType.If    => BuildIfDisplay(indent),
            _                   => $"{indent}?",
        };
    }

    private string BuildIfDisplay(string indent)
    {
        var cond = Condition?.ToDisplayString() ?? "…";
        var then = ThenBody.Count == 1
            ? ThenBody[0].ToDisplayString().TrimStart()
            : $"({ThenBody.Count} statements)";

        var result = $"{indent}? if {cond}: {then}";

        if (ElseBody is { Count: > 0 })
        {
            var els = ElseBody.Count == 1
                ? ElseBody[0].ToDisplayString().TrimStart()
                : $"({ElseBody.Count} statements)";
            result += $"  else: {els}";
        }

        return result;
    }

    public override string ToString() => ToDisplayString();
}
