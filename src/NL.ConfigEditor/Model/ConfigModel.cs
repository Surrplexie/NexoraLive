namespace NL.ConfigEditor.Model;

public class ConfigModel
{
    public List<HotkeyEntry> Hotkeys { get; set; } = [];
    public List<EventEntry> Events { get; set; } = [];
}
