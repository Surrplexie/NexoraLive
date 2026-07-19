namespace NL.ConfigEditor.Model;

public class SimpleConditionEntry
{
    public string Left { get; set; } = "player.health";
    public string Op { get; set; } = ">";
    public string Right { get; set; } = "0";

    public override string ToString() => $"{Left} {Op} {Right}";
}
