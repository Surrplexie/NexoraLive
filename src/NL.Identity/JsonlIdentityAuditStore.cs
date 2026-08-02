using System.Text.Json;
using NL.Identity.Core;

namespace NL.Identity;

public sealed class JsonlIdentityAuditStore : IIdentityAuditStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonlIdentityAuditStore(string? path = null) =>
        _path = path ?? NlIdentityPaths.AuditLog;

    public void Append(NlIdentityAuditEvent entry)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var line = JsonSerializer.Serialize(new
        {
            kind = entry.Kind.ToString(),
            accountId = entry.AccountId,
            platformKey = entry.PlatformKey,
            message = entry.Message,
            ts = entry.TimestampUtc,
        });

        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    public IReadOnlyList<NlIdentityAuditEvent> ReadRecent(int count = 100)
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<NlIdentityAuditEvent>();
        }

        lock (_lock)
        {
            var lines = File.ReadAllLines(_path);
            return lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .TakeLast(count)
                .Select(ParseLine)
                .Where(e => e is not null)
                .Cast<NlIdentityAuditEvent>()
                .ToList();
        }
    }

    private static NlIdentityAuditEvent? ParseLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var kind = Enum.Parse<NlIdentityAuditKind>(root.GetProperty("kind").GetString()!);
            var accountId = root.TryGetProperty("accountId", out var a) ? a.GetString() : null;
            var platformKey = root.TryGetProperty("platformKey", out var p) ? p.GetString() : null;
            var message = root.GetProperty("message").GetString() ?? "";
            var ts = root.GetProperty("ts").GetDateTimeOffset();
            return new NlIdentityAuditEvent(kind, accountId, platformKey, message, ts);
        }
        catch
        {
            return null;
        }
    }
}
