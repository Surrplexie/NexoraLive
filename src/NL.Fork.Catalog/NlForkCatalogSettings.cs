using NL.Fork.Catalog.Core;

namespace NL.Fork.Catalog;

public sealed class NlForkCatalogSettings
{
    public const string EnabledVariable = "NL_FORK_CATALOG_ENABLED";

    public bool Enabled { get; init; }

    public int DefaultMaxMajorsPerGame { get; init; } = 3;

    public static NlForkCatalogSettings LoadFromEnvironment()
    {
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable(EnabledVariable),
            "1",
            StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable(EnabledVariable),
                "true",
                StringComparison.OrdinalIgnoreCase);

        var maxMajors = int.TryParse(Environment.GetEnvironmentVariable("NL_FORK_CATALOG_MAX_MAJORS"), out var max)
            ? Math.Max(1, max)
            : 3;

        return new NlForkCatalogSettings
        {
            Enabled = enabled,
            DefaultMaxMajorsPerGame = maxMajors,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        maxMajorsPerGame = DefaultMaxMajorsPerGame,
        manifestPath = NlForkCatalogPaths.Manifest,
        storePath = NlForkCatalogPaths.Root,
    };
}

public sealed class NlForkCatalogHost
{
    public NlForkCatalogHost(NlForkCatalogSettings settings, string? manifestPath = null)
    {
        Settings = settings;
        NlForkCatalogPaths.EnsureRoot();

        Repository = new JsonForkCatalogRepository(manifestPath);
        Validator = new ForkCatalogValidator(Repository);
        Governance = new ForkCatalogGovernance(Repository);
        ModResolver = new ForkModSlotResolver(Repository);
        Catalog = new ForkCatalogService(Repository, Validator, Governance, ModResolver);

        EnsureDefaultQuota();
    }

    public NlForkCatalogSettings Settings { get; }

    public IForkCatalogRepository Repository { get; }

    public ForkCatalogValidator Validator { get; }

    public ForkCatalogGovernance Governance { get; }

    public ForkModSlotResolver ModResolver { get; }

    public ForkCatalogService Catalog { get; }

    private void EnsureDefaultQuota()
    {
        var manifest = Repository.Load();
        if (manifest.MaxMajorsPerGame <= 0)
        {
            manifest.MaxMajorsPerGame = Settings.DefaultMaxMajorsPerGame;
            Repository.Save(manifest);
        }
    }
}
