namespace NL.NleEditor.Model;

public class EventEntry
{
    public string Name { get; set; } = "newEvent";
    public List<StatementEntry> Statements { get; set; } = [];

    public override string ToString() => Name;
}
