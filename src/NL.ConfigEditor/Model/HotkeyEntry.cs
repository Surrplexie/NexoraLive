namespace NL.ConfigEditor.Model;

public class HotkeyEntry
{
    public string Combo { get; set; } = "";
    public string Action { get; set; } = "";

    public override string ToString() => $"\"{Combo}\" → {Action}";
}
