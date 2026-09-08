using System.Text.Json;

namespace NL.Fork.Core;

/// <summary>Persisted fork status for operator dashboards (written by NL.Fork.Runtime).</summary>
public static class ForkStatusFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Write(string path, ForkRuntimeStatus status, bool connected = true, ForkGameKind? game = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var payload = new
        {
            updatedAtUtc = DateTimeOffset.UtcNow,
            connected,
            game = game?.ToString().ToLowerInvariant(),
            sessionStarted = status.SessionStarted,
            connectedPlayers = status.ConnectedPlayers,
            players = status.Players,
            recentActions = status.RecentActions,
            modIds = status.LoadedModIds,
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static ForkRuntimeStatusSnapshot? TryRead(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            return new ForkRuntimeStatusSnapshot(
                root.TryGetProperty("updatedAtUtc", out var u) && u.TryGetDateTimeOffset(out var updated)
                    ? updated
                    : null,
                root.GetProperty("sessionStarted").GetBoolean(),
                root.GetProperty("connectedPlayers").GetInt32());
        }
        catch
        {
            return null;
        }
    }
}

public sealed record ForkRuntimeStatusSnapshot(
    DateTimeOffset? UpdatedAtUtc,
    bool SessionStarted,
    int ConnectedPlayers);
