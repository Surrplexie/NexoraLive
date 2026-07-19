using System.Text.Json;
using System.Text.Json.Serialization;

namespace NL.HotkeyDaemon.Core;

/// <summary>
/// Loads the "combo -> action name" bindings from a small JSON file (see
/// samples/hotkeys/hotkeys.json). Deliberately tolerant: a bad individual entry becomes a
/// warning and is skipped rather than crashing the whole daemon over one typo.
/// </summary>
public sealed class HotkeyConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HotkeyConfig(List<HotkeyBinding> bindings, List<string> warnings)
    {
        Bindings = bindings;
        Warnings = warnings;
    }

    public IReadOnlyList<HotkeyBinding> Bindings { get; }

    public IReadOnlyList<string> Warnings { get; }

    public static HotkeyConfig Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<HotkeyConfigDto>(json, JsonOptions) ?? new HotkeyConfigDto();
        var bindings = new List<HotkeyBinding>();
        var warnings = new List<string>();
        var seenCombos = new HashSet<HotkeyCombo>();

        foreach (var raw in dto.Bindings ?? new List<HotkeyBindingDto>())
        {
            if (string.IsNullOrWhiteSpace(raw.Action))
            {
                warnings.Add($"binding for combo '{raw.Combo}' has no action name; skipped");
                continue;
            }

            if (!HotkeyCombo.TryParse(raw.Combo ?? string.Empty, out var combo, out var error))
            {
                warnings.Add($"invalid combo '{raw.Combo}': {error}; skipped");
                continue;
            }

            if (!seenCombos.Add(combo!))
            {
                warnings.Add($"combo '{combo}' is bound more than once; only the first binding is kept");
                continue;
            }

            bindings.Add(new HotkeyBinding(combo!, raw.Action));
        }

        return new HotkeyConfig(bindings, warnings);
    }

    public static HotkeyConfig LoadFromFile(string path) => Parse(File.ReadAllText(path));

    private sealed class HotkeyConfigDto
    {
        [JsonPropertyName("bindings")]
        public List<HotkeyBindingDto>? Bindings { get; set; }
    }

    private sealed class HotkeyBindingDto
    {
        [JsonPropertyName("combo")]
        public string? Combo { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }
    }
}
