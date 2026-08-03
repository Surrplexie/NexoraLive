namespace NL.Fork.Catalog.Core;

/// <summary>One major-version fork snapshot row in the NL catalog.</summary>
public sealed record ForkCatalogEntry(
    string GameId,
    string DisplayName,
    string MajorVersion,
    string ImageDigest,
    PartnershipTier Tier = PartnershipTier.AtOwnRisk,
    string? MinClientVersion = null,
    string? DefaultNleTemplate = null,
    bool NoProgressTransfer = true,
    ForkCatalogEntryStatus Status = ForkCatalogEntryStatus.Active,
    DateTimeOffset? RegisteredAtUtc = null,
    DateTimeOffset? DeprecatedAtUtc = null,
    string? LegalNotice = null,
    string? DockerImage = null,
    bool IsDefaultStable = false)
{
    public string CatalogKey => ForkCatalogKey.Create(GameId, MajorVersion);

    public string EffectiveLegalNotice =>
        string.IsNullOrWhiteSpace(LegalNotice)
            ? PartnershipTierLabels.LegalNotice(Tier)
            : LegalNotice!;
}

/// <summary>Verified server-side mod in the NL mod hub — hash-checked before bake-in.</summary>
public sealed record ModHubEntry(
    string Id,
    string Sha256,
    string? Description = null,
    Dictionary<string, double>? Props = null);

public sealed class ForkCatalogManifest
{
    /// <summary>Max active major rows per gameId before oldest is auto-deprecated.</summary>
    public int MaxMajorsPerGame { get; set; } = 3;

    public List<ForkCatalogEntry> Entries { get; set; } = [];

    public List<ModHubEntry> ModHub { get; set; } = [];
}

public sealed record ForkCatalogSelection(
    string GameId,
    string MajorVersion,
    IReadOnlyList<string> AttachedModIds)
{
    public string CatalogKey => ForkCatalogKey.Create(GameId, MajorVersion);
}

public static class ForkCatalogKey
{
    public static string Create(string gameId, string majorVersion)
    {
        var major = ForkMajorVersion.TryNormalize(majorVersion, out var n) ? n : majorVersion.Trim();
        return $"{gameId.Trim()}@{major}";
    }
}

public sealed record ForkCatalogValidationResult(
    bool IsValid,
    string? Error = null,
    ForkCatalogEntry? Entry = null)
{
    public static ForkCatalogValidationResult Ok(ForkCatalogEntry entry) =>
        new(true, Entry: entry);

    public static ForkCatalogValidationResult Fail(string error) =>
        new(false, error);
}

public sealed record ForkCatalogResolveResult(
    ForkCatalogEntry Entry,
    string ResolvedNleTemplate,
    IReadOnlyList<string> AttachedModIds,
    IReadOnlyList<ModHubEntry> ResolvedMods);
