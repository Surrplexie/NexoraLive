using System.Globalization;
using System.Text.Json;
using NL.Core;
using NL.Server.Core.Integration;

namespace NL.Server.Core.Generic;

/// <summary>
/// Parses one NDJSON line into a <see cref="SessionEvent"/>. This is the universal "any game"
/// integration: a game/mod/plugin writes a line like
/// <c>{"event":"shoot","player":"Alice","props":{"weapon.damage":12}}</c> and NLServer can
/// enforce a <c>.nle</c> config against it with no C# adapter for that specific title.
///
/// Required JSON fields: <c>event</c> (string). Optional: <c>nl</c> (protocol version, must be 1 if present),
/// <c>player</c> (string),
/// <c>props</c> (object of number values), <c>ts</c> (Unix milliseconds number — used by
/// Phase 5 anti-cheat for deterministic rate/teleport windows in replay). Empty/whitespace
/// lines and <c>{"event":null}</c> return null (skip). Malformed JSON throws
/// <see cref="FormatException"/>.
/// </summary>
public static class GenericJsonLineParser
{
    public static SessionEvent? TryParse(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return null;
        }

        var trimmed = rawLine.TrimStart();
        if (trimmed.StartsWith('#'))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(rawLine);
        var root = doc.RootElement;

        if (root.TryGetProperty("nl", out var nlProp) && nlProp.ValueKind == JsonValueKind.Number)
        {
            var nlVersion = nlProp.GetInt32();
            if (nlVersion != NlIntegrationProtocol.Version)
            {
                throw new FormatException(
                    $"Unsupported nl protocol version {nlVersion} (expected {NlIntegrationProtocol.Version}).");
            }
        }

        if (!root.TryGetProperty("event", out var eventProp) || eventProp.ValueKind != JsonValueKind.String)
        {
            throw new FormatException("NDJSON line must include a string \"event\" field.");
        }

        var eventName = eventProp.GetString();
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return null;
        }

        string? player = null;
        if (root.TryGetProperty("player", out var playerProp) && playerProp.ValueKind == JsonValueKind.String)
        {
            player = playerProp.GetString();
        }

        var props = new Dictionary<string, double>();
        if (root.TryGetProperty("props", out var propsProp) && propsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propsProp.EnumerateObject())
            {
                props[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.True => 1.0,
                    JsonValueKind.False => 0.0,
                    JsonValueKind.String when double.TryParse(
                        property.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    _ => throw new FormatException(
                        $"props.\"{property.Name}\" must be a number (or boolean/numeric string)."),
                };
            }
        }

        DateTimeOffset? timestamp = null;
        if (root.TryGetProperty("ts", out var tsProp))
        {
            timestamp = tsProp.ValueKind switch
            {
                JsonValueKind.Number => DateTimeOffset.FromUnixTimeMilliseconds(tsProp.GetInt64()),
                JsonValueKind.String when DateTimeOffset.TryParse(
                    tsProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
                _ => throw new FormatException("\"ts\" must be Unix milliseconds (number) or an ISO-8601 string."),
            };
        }

        return new SessionEvent(new GameEvent(eventName, props), player, timestamp);
    }
}
