namespace NL.Kenshi.Bridge;

/// <summary>NL Integration Spec v1 decision returned to the Kenshi host (propose-then-commit).</summary>
public sealed record NlKenshiDecision(bool Allowed, string? Message, string? Action)
{
    public static NlKenshiDecision Allow() => new(true, null, null);

    public static NlKenshiDecision Block(string? message, string? action = "kick") =>
        new(false, message, action);
}

/// <summary>Inbound action line from the NL session bus.</summary>
public sealed record NlKenshiAction(
    string Action,
    string Player,
    string Event,
    string Decision,
    string Message);

/// <summary>Evaluates an outpost event against NL (embedded fake or live WebSocket bus).</summary>
public interface INlKenshiDecisionBus
{
    NlKenshiDecision Evaluate(string eventName, string player, IReadOnlyDictionary<string, double> props);
}

/// <summary>Minimal JSON action-line parser (no extra packages — matches Paper NlWebSocketBridge).</summary>
public static class NlKenshiActionParser
{
    public static NlKenshiAction? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Contains("\"action\"", StringComparison.Ordinal))
        {
            return null;
        }

        var action = ExtractString(line, "action");
        var player = ExtractString(line, "player");
        var eventName = ExtractString(line, "event");
        if (action is null || player is null || eventName is null)
        {
            return null;
        }

        return new NlKenshiAction(
            action,
            player,
            eventName,
            ExtractString(line, "decision") ?? "",
            ExtractString(line, "message") ?? "");
    }

    public static string BuildEventLine(string eventName, string player, IReadOnlyDictionary<string, double> props)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var propsJson = "";
        if (props.Count > 0)
        {
            var parts = props.Select(p => $"\"{Escape(p.Key)}\":{p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            propsJson = ",\"props\":{" + string.Join(",", parts) + "}";
        }

        return $"{{\"nl\":1,\"event\":\"{Escape(eventName)}\",\"player\":\"{Escape(player)}\",\"ts\":{ts}{propsJson}}}";
    }

    public static NlKenshiDecision ToDecision(NlKenshiAction? action)
    {
        if (action is null)
        {
            return NlKenshiDecision.Allow();
        }

        var block = string.Equals(action.Decision, "Block", StringComparison.OrdinalIgnoreCase);
        return block
            ? NlKenshiDecision.Block(action.Message, action.Action)
            : NlKenshiDecision.Allow();
    }

    private static string? ExtractString(string json, string key)
    {
        var needle = "\"" + key + "\":\"";
        var start = json.IndexOf(needle, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += needle.Length;
        var end = json.IndexOf('"', start);
        return end < 0 ? null : json[start..end].Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
