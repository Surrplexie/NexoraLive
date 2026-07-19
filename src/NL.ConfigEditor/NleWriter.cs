using System.Text;
using NL.ConfigEditor.Model;

namespace NL.ConfigEditor;

/// <summary>
/// Converts the in-memory <see cref="ConfigModel"/> to a valid <c>.nle</c> source string.
/// The result is always parseable by <c>NL.Core.Parser</c>; the two classes are kept in sync.
/// </summary>
public static class NleWriter
{
    public static string Write(ConfigModel model)
    {
        var sb = new StringBuilder();

        if (model.Hotkeys.Count > 0)
        {
            sb.AppendLine("# ── Hotkey bindings ──────────────────────────────────────────────────────────");
            foreach (var h in model.Hotkeys)
            {
                sb.AppendLine($"hotkey \"{h.Combo}\": {h.Action}");
            }

            sb.AppendLine();
        }

        if (model.Events.Count > 0)
        {
            sb.AppendLine("# ── NLEvent rules ────────────────────────────────────────────────────────────");
            foreach (var evt in model.Events)
            {
                sb.AppendLine($"event {evt.Name}:");
                WriteStatements(sb, evt.Statements, depth: 1);
                sb.AppendLine();
            }
        }

        // Trim trailing blank lines but keep a single trailing newline.
        var text = sb.ToString().TrimEnd('\r', '\n');
        return text.Length > 0 ? text + Environment.NewLine : string.Empty;
    }

    private static void WriteStatements(StringBuilder sb, List<StatementEntry> statements, int depth)
    {
        var indent = new string(' ', depth * 4);

        foreach (var stmt in statements)
        {
            switch (stmt.Type)
            {
                case StatementType.Allow:
                    sb.AppendLine($"{indent}allow");
                    break;

                case StatementType.Block:
                    sb.AppendLine($"{indent}block");
                    break;

                case StatementType.Deny:
                    sb.AppendLine($"{indent}deny");
                    break;

                case StatementType.Warn:
                    var msg = (stmt.WarnMessage ?? "").Replace("\"", "'");
                    sb.AppendLine($"{indent}warn \"{msg}\"");
                    break;

                case StatementType.If:
                    var condText = WriteCondition(stmt.Condition);
                    sb.AppendLine($"{indent}if {condText}:");

                    var thenBody = stmt.ThenBody.Count > 0
                        ? stmt.ThenBody
                        : [new StatementEntry { Type = StatementType.Allow }];
                    WriteStatements(sb, thenBody, depth + 1);

                    if (stmt.ElseBody is { Count: > 0 })
                    {
                        sb.AppendLine($"{indent}else:");
                        WriteStatements(sb, stmt.ElseBody, depth + 1);
                    }
                    break;
            }
        }
    }

    private static string WriteCondition(ConditionEntry? cond)
    {
        if (cond is null || cond.Parts.Count == 0)
        {
            return "true == true";
        }

        var parts = new List<string>();
        for (var i = 0; i < cond.Parts.Count; i++)
        {
            var p = cond.Parts[i];
            parts.Add($"{p.Left} {p.Op} {p.Right}");

            if (i < cond.Joins.Count)
            {
                parts.Add(cond.Joins[i]);
            }
        }

        return string.Join(" ", parts);
    }
}
