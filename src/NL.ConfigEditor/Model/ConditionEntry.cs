using System.Text;

namespace NL.ConfigEditor.Model;

/// <summary>
/// One or more simple comparisons joined by "and"/"or".
/// Parts[0] Joins[0] Parts[1] Joins[1] Parts[2] …
/// </summary>
public class ConditionEntry
{
    public List<SimpleConditionEntry> Parts { get; set; } = [new()];

    /// <summary>"and" or "or" — length is always Parts.Count - 1.</summary>
    public List<string> Joins { get; set; } = [];

    public string ToDisplayString()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Parts.Count; i++)
        {
            if (i > 0) sb.Append($" {Joins[i - 1]} ");
            sb.Append(Parts[i]);
        }
        return sb.ToString();
    }

    public override string ToString() => ToDisplayString();
}
