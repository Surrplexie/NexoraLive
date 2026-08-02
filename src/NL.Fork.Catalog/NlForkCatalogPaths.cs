namespace NL.Fork.Catalog;

public static class NlForkCatalogPaths
{
    public static string Root
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("NL_FORK_CATALOG_ROOT");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Path.GetFullPath(overrideRoot);
            }

            return Path.Combine(NL.Core.NlPaths.Root, "fork-catalog");
        }
    }

    public static string Manifest =>
        Environment.GetEnvironmentVariable("NL_FORK_CATALOG_MANIFEST")
        ?? Path.Combine(Root, "catalog.json");

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
