using NL.Fork.Core;
using NL.Fork.Catalog.Core;

namespace NL.Fork.Catalog;

public sealed class ForkCatalogService
{
    private readonly IForkCatalogRepository _repository;
    private readonly ForkCatalogValidator _validator;
    private readonly ForkCatalogGovernance _governance;
    private readonly ForkModSlotResolver _modResolver;

    public ForkCatalogService(
        IForkCatalogRepository repository,
        ForkCatalogValidator validator,
        ForkCatalogGovernance governance,
        ForkModSlotResolver modResolver)
    {
        _repository = repository;
        _validator = validator;
        _governance = governance;
        _modResolver = modResolver;
    }

    public ForkCatalogManifest GetManifest() => _repository.Load();

    public IReadOnlyList<ForkCatalogEntry> ListGames(bool includeDeprecated = false)
    {
        var manifest = _repository.Load();
        return manifest.Entries
            .Where(e => includeDeprecated || e.Status == ForkCatalogEntryStatus.Active)
            .OrderBy(e => e.GameId)
            .ThenByDescending(e => e.MajorVersion)
            .ToList();
    }

    public ForkCatalogEntry? GetEntry(string gameId, string majorVersion)
    {
        if (!ForkMajorVersion.TryNormalize(majorVersion, out var major))
        {
            return null;
        }

        return _repository.Load().Entries.FirstOrDefault(e =>
            string.Equals(e.GameId, gameId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.MajorVersion, major, StringComparison.OrdinalIgnoreCase));
    }

    public ForkCatalogValidationResult ValidateSelection(ForkCatalogSelection selection) =>
        _validator.ValidateSelection(selection);

    public ForkCatalogValidationResult ValidateClientMajor(
        string gameId,
        string? clientMajor,
        string expectedMajor) =>
        _validator.ValidateClientMajor(gameId, clientMajor, expectedMajor);

    public ForkCatalogResolveResult ResolveSelection(ForkCatalogSelection selection, string? samplesRoot = null)
    {
        var validation = _validator.ValidateSelection(selection);
        if (!validation.IsValid || validation.Entry is null)
        {
            throw new InvalidOperationException(validation.Error ?? "Invalid catalog selection.");
        }

        var nleTemplate = ResolveNlePath(validation.Entry.DefaultNleTemplate, samplesRoot);
        var mods = selection.AttachedModIds
            .Select(id => _repository.Load().ModHub.First(m =>
                string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new ForkCatalogResolveResult(
            validation.Entry,
            nleTemplate,
            selection.AttachedModIds,
            mods);
    }

    public ForkCatalogEntry RegisterEntry(ForkCatalogEntry entry)
    {
        var validation = _validator.ValidateEntryForRegistration(entry);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error ?? "Invalid catalog entry.");
        }

        return _governance.Register(entry);
    }

    public void SaveManifest(ForkCatalogManifest manifest) => _repository.Save(manifest);

    public ForkModManifest ResolveMods(IReadOnlyList<string> modIds) => _modResolver.Resolve(modIds);

    private static string ResolveNlePath(string? template, string? samplesRoot)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return "";
        }

        if (Path.IsPathRooted(template) && File.Exists(template))
        {
            return template;
        }

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(samplesRoot))
        {
            candidates.Add(Path.Combine(samplesRoot, template));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return template;
    }
}
