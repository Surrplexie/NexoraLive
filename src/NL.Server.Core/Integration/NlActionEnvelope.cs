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
}
