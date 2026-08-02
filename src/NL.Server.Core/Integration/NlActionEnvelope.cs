using System.Text.Json;
using NL.Core;
using NL.Server.Core;

namespace NL.Server.Core.Integration;

/// <summary>Standard NL → game action line (one NDJSON object per line).</summary>
public static class NlActionEnvelope
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(
        SessionEvent sessionEvent,
        ActionResult result,
        string? actionOverride = null,
        DateTimeOffset? timestamp = null)
    {
        var action = actionOverride ?? NlStandardActions.ChooseAction(sessionEvent);
        var payload = new Dictionary<string, object?>
        {
            ["nl"] = NlIntegrationProtocol.Version,
            ["type"] = "action",
            ["action"] = action,
            ["player"] = sessionEvent.PlayerName ?? "",
            ["event"] = sessionEvent.Event.Name,
            ["decision"] = result.Decision.ToString(),
            ["message"] = result.Message ?? "",
            ["ts"] = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds(),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>Parses an NL → game action line from the session bus.</summary>
    public static NlActionMessage? TryParse(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(rawLine.Trim());
        var root = doc.RootElement;

        if (root.TryGetProperty("type", out var typeProp)
            && typeProp.ValueKind == JsonValueKind.String
            && !string.Equals(typeProp.GetString(), "action", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!root.TryGetProperty("action", out var actionProp) || actionProp.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var action = actionProp.GetString() ?? "";
        var player = root.TryGetProperty("player", out var playerProp) && playerProp.ValueKind == JsonValueKind.String
            ? playerProp.GetString() ?? ""
            : "";
        var eventName = root.TryGetProperty("event", out var eventProp) && eventProp.ValueKind == JsonValueKind.String
            ? eventProp.GetString() ?? ""
            : "";
        var decision = root.TryGetProperty("decision", out var decisionProp) && decisionProp.ValueKind == JsonValueKind.String
            ? decisionProp.GetString() ?? ""
            : "";
        var message = root.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String
            ? messageProp.GetString() ?? ""
            : "";

        long ts = 0;
        if (root.TryGetProperty("ts", out var tsProp) && tsProp.ValueKind == JsonValueKind.Number)
        {
            ts = tsProp.GetInt64();
        }

        return new NlActionMessage(action, player, eventName, decision, message, ts);
    }
}

/// <summary>Deserialized NL integration action sent to a fork runtime.</summary>
public sealed record NlActionMessage(
    string Action,
    string Player,
    string Event,
    string Decision,
    string Message,
    long TimestampMs);
