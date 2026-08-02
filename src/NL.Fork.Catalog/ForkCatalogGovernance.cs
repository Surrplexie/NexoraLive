using NL.Fork.Catalog.Core;

namespace NL.Fork.Catalog;

/// <summary>
/// Enforces max-N majors per title — deprecates oldest active major when quota exceeded.
/// </summary>
public sealed class ForkCatalogGovernance
{
    private readonly IForkCatalogRepository _repository;
    private readonly Func<DateTimeOffset> _clock;

    public ForkCatalogGovernance(
        IForkCatalogRepository repository,
        Func<DateTimeOffset>? clock = null)
    {
        _repository = repository;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public ForkCatalogEntry Register(ForkCatalogEntry entry)
    {
        if (!ForkMajorVersion.TryNormalize(entry.MajorVersion, out var major))
        {
            throw new ArgumentException($"Invalid major version '{entry.MajorVersion}'.", nameof(entry));
        }

        var manifest = _repository.Load();
        var now = _clock();
        var normalized = entry with
        {
            MajorVersion = major,
            RegisteredAtUtc = entry.RegisteredAtUtc ?? now,
            Status = ForkCatalogEntryStatus.Active,
            DeprecatedAtUtc = null,
        };

        manifest.Entries.RemoveAll(e =>
            string.Equals(e.GameId, normalized.GameId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.MajorVersion, major, StringComparison.OrdinalIgnoreCase));

        manifest.Entries.Add(normalized);
        ApplyQuota(manifest, normalized.GameId, now);
        _repository.Save(manifest);
        return normalized;
    }

    public IReadOnlyList<ForkCatalogEntry> ApplyQuota(ForkCatalogManifest manifest, string gameId, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? _clock();
        var active = manifest.Entries
            .Where(e => string.Equals(e.GameId, gameId, StringComparison.OrdinalIgnoreCase)
                        && e.Status == ForkCatalogEntryStatus.Active)
            .OrderBy(e => e.RegisteredAtUtc ?? DateTimeOffset.MinValue)
            .ToList();

        var deprecated = new List<ForkCatalogEntry>();
        while (active.Count > manifest.MaxMajorsPerGame)
        {
            var oldest = active[0];
            var updated = oldest with
            {
                Status = ForkCatalogEntryStatus.Deprecated,
                DeprecatedAtUtc = now,
            };
            ReplaceEntry(manifest, updated);
            deprecated.Add(updated);
            active.RemoveAt(0);
        }

        return deprecated;
    }

    private static void ReplaceEntry(ForkCatalogManifest manifest, ForkCatalogEntry updated)
    {
        manifest.Entries.RemoveAll(e => e.CatalogKey == updated.CatalogKey);
        manifest.Entries.Add(updated);
    }
}
