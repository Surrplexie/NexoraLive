using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class JsonLegalComplianceAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    public JsonLegalComplianceAuditStore(string? path = null) =>
        _path = path ?? NlFleetPaths.LegalComplianceAudit;

    public IReadOnlyList<LegalComplianceAuditEntry> ListRecent(int max = 100)
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<LegalComplianceAuditEntry>>(File.ReadAllText(_path), JsonOptions) ?? [];
                return list.OrderByDescending(e => e.RecordedAtUtc).Take(max).ToList();
            }
            catch
            {
                return [];
            }
        }
    }

    public LegalComplianceAuditEntry Record(string action, string subjectId, string? detail = null)
    {
        var entry = new LegalComplianceAuditEntry(
            action,
            subjectId,
            detail,
            DateTimeOffset.UtcNow);

        lock (_lock)
        {
            NlFleetPaths.EnsureRoot();
            var list = File.Exists(_path)
                ? JsonSerializer.Deserialize<List<LegalComplianceAuditEntry>>(File.ReadAllText(_path), JsonOptions) ?? []
                : [];
            list.Add(entry);
            if (list.Count > 5000)
            {
                list = list.OrderByDescending(e => e.RecordedAtUtc).Take(5000).ToList();
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(list, JsonOptions));
        }

        return entry;
    }
}
