namespace NL.Server;

/// <summary>Preset game events visitors can fire in the public demo (Phase H).</summary>
public static class NlSpectatorScenarios
{
    public sealed record Scenario(
        string Id,
        string Label,
        string Description,
        string Event,
        string Player,
        IReadOnlyDictionary<string, double> Props,
        string ExpectedDecision);

    private static readonly Scenario[] All =
    [
        new(
            "shoot",
            "Player shoots",
            "PvP shooting is blocked by demo.nle.",
            "shoot",
            "Visitor",
            new Dictionary<string, double> { ["weapon.damage"] = 12 },
            "Block"),
        new(
            "caps-chat",
            "ALL CAPS chat",
            "Long all-caps chat triggers warn + block.",
            "playerChat",
            "Visitor",
            new Dictionary<string, double>
            {
                ["chat.length"] = 22,
                ["chat.capsRatio"] = 1,
                ["chat.isCommand"] = 0,
            },
            "Block"),
        new(
            "clean-chat",
            "Normal chat",
            "Short lowercase chat is allowed.",
            "playerChat",
            "Visitor",
            new Dictionary<string, double>
            {
                ["chat.length"] = 8,
                ["chat.capsRatio"] = 0,
                ["chat.isCommand"] = 0,
            },
            "Allow"),
        new(
            "early-respawn",
            "Respawn while alive",
            "Respawn with health > 0 is blocked.",
            "respawn",
            "Visitor",
            new Dictionary<string, double> { ["player.health"] = 40 },
            "Block"),
        new(
            "join",
            "Player joins",
            "Join shows a welcome warn then allows.",
            "playerJoin",
            "Visitor",
            new Dictionary<string, double> { ["player.alive"] = 1 },
            "Allow"),
    ];

    public static IReadOnlyList<Scenario> List() => All;

    public static Scenario? Find(string id) =>
        All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static string ToNdjsonLine(Scenario scenario, long? timestampMs = null)
    {
        var ts = timestampMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var propsPart = string.Join(
            ",",
            scenario.Props.Select(kvp =>
                $"\"{kvp.Key}\":{kvp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        if (propsPart.Length == 0)
        {
            return $"{{\"nl\":1,\"event\":\"{scenario.Event}\",\"player\":\"{scenario.Player}\",\"ts\":{ts}}}";
        }

        return $"{{\"nl\":1,\"event\":\"{scenario.Event}\",\"player\":\"{scenario.Player}\",\"ts\":{ts},\"props\":{{ {propsPart} }}}}";
    }
}
