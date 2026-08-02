using NL.Fork.Catalog.Core;

namespace NL.Fork.Catalog;

public sealed class ForkCatalogValidator
{
    private readonly IForkCatalogRepository _repository;

    public ForkCatalogValidator(IForkCatalogRepository repository) => _repository = repository;

    public ForkCatalogValidationResult ValidateSelection(ForkCatalogSelection selection)
    {
        if (string.IsNullOrWhiteSpace(selection.GameId))
        {
            return ForkCatalogValidationResult.Fail("gameId required.");
        }

        if (!ForkMajorVersion.TryNormalize(selection.MajorVersion, out var major))
        {
            return ForkCatalogValidationResult.Fail(
                $"Major version '{selection.MajorVersion}' is invalid — only X.0 majors are cataloged.");
        }

        var manifest = _repository.Load();
        var entry = manifest.Entries.FirstOrDefault(e =>
            string.Equals(e.GameId, selection.GameId.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.MajorVersion, major, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return ForkCatalogValidationResult.Fail(
                $"Unknown catalog entry '{selection.GameId}@{major}'. Register it in the fork catalog first.");
        }

        if (entry.Status == ForkCatalogEntryStatus.Deprecated)
        {
            return ForkCatalogValidationResult.Fail(
                $"Catalog entry '{entry.CatalogKey}' is deprecated — migrate to a supported major version.");
        }

        foreach (var modId in selection.AttachedModIds)
        {
            if (manifest.ModHub.All(m => !string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase)))
            {
                return ForkCatalogValidationResult.Fail($"Mod '{modId}' is not in the verified mod hub.");
            }
        }

        return ForkCatalogValidationResult.Ok(entry);
    }

    public ForkCatalogValidationResult ValidateClientMajor(
        string gameId,
        string? clientMajor,
        string expectedMajor)
    {
        if (string.IsNullOrWhiteSpace(clientMajor))
        {
            return ForkCatalogValidationResult.Fail("Client major version is required for catalog-gated sessions.");
        }

        if (!ForkMajorVersion.TryNormalize(clientMajor, out var normalizedClient))
        {
            return ForkCatalogValidationResult.Fail(
                $"Client version '{clientMajor}' is not a major version (expected X.0).");
        }

        if (!ForkMajorVersion.TryNormalize(expectedMajor, out var normalizedExpected))
        {
            return ForkCatalogValidationResult.Fail($"Session major version '{expectedMajor}' is invalid.");
        }

        if (!string.Equals(normalizedClient, normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            return ForkCatalogValidationResult.Fail(
                $"Client major {normalizedClient} does not match session {normalizedExpected} for '{gameId}'.");
        }

        return ValidateSelection(new ForkCatalogSelection(gameId, normalizedExpected, []));
    }

    public ForkCatalogValidationResult ValidateEntryForRegistration(ForkCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.GameId))
        {
            return ForkCatalogValidationResult.Fail("gameId required.");
        }

        if (string.IsNullOrWhiteSpace(entry.DisplayName))
        {
            return ForkCatalogValidationResult.Fail("displayName required.");
        }

        if (string.IsNullOrWhiteSpace(entry.ImageDigest))
        {
            return ForkCatalogValidationResult.Fail("imageDigest required.");
        }

        if (!ForkMajorVersion.TryNormalize(entry.MajorVersion, out _))
        {
            return ForkCatalogValidationResult.Fail(
                $"Major version '{entry.MajorVersion}' must be X.0 — patch minors (1.2, 1.4) are not catalog rows.");
        }

        return ForkCatalogValidationResult.Ok(entry);
    }
}
