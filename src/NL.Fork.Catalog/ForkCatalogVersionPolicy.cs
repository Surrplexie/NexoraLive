using NL.Fork.Catalog.Core;

namespace NL.Fork.Catalog;

/// <summary>
/// Default everyone to the latest stable catalog major; custom major pick is beta/paid.
/// </summary>
public sealed class ForkCatalogVersionPolicy
{
    private readonly ForkCatalogService _catalog;
    private readonly NlForkCatalogSettings _settings;

    public ForkCatalogVersionPolicy(ForkCatalogService catalog, NlForkCatalogSettings settings)
    {
        _catalog = catalog;
        _settings = settings;
    }

    public bool DefaultToLatestStable => _settings.DefaultToLatestStable;

    public bool CustomMajorVersionBetaEnabled => _settings.CustomMajorVersionBetaEnabled;

    public ForkCatalogEntry? ResolveLatestStableEntry(string gameId) =>
        _catalog.ResolveLatestStableEntry(gameId);

    public bool IsLatestStable(string gameId, string majorVersion)
    {
        var latest = ResolveLatestStableEntry(gameId);
        if (latest is null || !ForkMajorVersion.TryNormalize(majorVersion, out var normalized))
        {
            return false;
        }

        return string.Equals(latest.MajorVersion, normalized, StringComparison.OrdinalIgnoreCase);
    }

    public ForkCatalogSelection ResolveSelection(
        string gameId,
        string? requestedMajor,
        IReadOnlyList<string> attachedModIds,
        bool allowCustomMajorForStreamer)
    {
        var trimmedGameId = gameId.Trim();
        var latest = ResolveLatestStableEntry(trimmedGameId)
            ?? throw new InvalidOperationException($"No active catalog entry for game '{trimmedGameId}'.");

        if (string.IsNullOrWhiteSpace(requestedMajor))
        {
            return new ForkCatalogSelection(trimmedGameId, latest.MajorVersion, attachedModIds);
        }

        if (!ForkMajorVersion.TryNormalize(requestedMajor, out var normalizedMajor))
        {
            throw new InvalidOperationException(
                $"Major version '{requestedMajor}' is invalid — only X.0 majors are cataloged.");
        }

        if (IsLatestStable(trimmedGameId, normalizedMajor))
        {
            return new ForkCatalogSelection(trimmedGameId, normalizedMajor, attachedModIds);
        }

        if (!_settings.DefaultToLatestStable)
        {
            return new ForkCatalogSelection(trimmedGameId, normalizedMajor, attachedModIds);
        }

        if (!_settings.CustomMajorVersionBetaEnabled)
        {
            throw new ForkCatalogVersionAccessException(
                "Custom major version selection is not enabled on this host.");
        }

        if (!allowCustomMajorForStreamer)
        {
            throw new ForkCatalogVersionAccessException(
                $"Major {normalizedMajor} is not the latest stable ({latest.MajorVersion}) for {trimmedGameId}. " +
                "Pinning older or alternate majors requires the beta/paid custom-major entitlement.");
        }

        return new ForkCatalogSelection(trimmedGameId, normalizedMajor, attachedModIds);
    }

    public IReadOnlyDictionary<string, string> BuildLatestStableIndex()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var gameId in _catalog.ListGames().Select(e => e.GameId).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var latest = ResolveLatestStableEntry(gameId);
            if (latest is not null)
            {
                map[gameId] = latest.MajorVersion;
            }
        }

        return map;
    }
}
