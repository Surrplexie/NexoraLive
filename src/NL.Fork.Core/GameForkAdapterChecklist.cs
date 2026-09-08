namespace NL.Fork.Core;

/// <summary>Phase T0 onboarding checklist result for one game adapter.</summary>
public sealed record GameForkAdapterCheckItem(string Id, string Description, bool Passed, string Detail);

public sealed record GameForkAdapterChecklistReport(
    string GameId,
    bool Passed,
    IReadOnlyList<GameForkAdapterCheckItem> Items);

/// <summary>
/// Validates that a game adapter has catalog, Docker, integration, .nle, dogfood, and smoke coverage.
/// </summary>
public static class GameForkAdapterChecklist
{
    public static GameForkAdapterChecklistReport Evaluate(
        string repoRoot,
        IGameForkAdapter adapter,
        string? catalogJson = null)
    {
        var items = new List<GameForkAdapterCheckItem>();

        void Check(string id, string description, bool passed, string detail) =>
            items.Add(new GameForkAdapterCheckItem(id, description, passed, detail));

        var catalogPath = Path.Combine(repoRoot, "samples", "fork", "catalog.json");
        var catalogText = catalogJson ?? (File.Exists(catalogPath) ? File.ReadAllText(catalogPath) : null);
        var catalogOk = catalogText is not null
            && catalogText.Contains($"\"gameId\": \"{adapter.GameId}\"", StringComparison.OrdinalIgnoreCase);
        if (!catalogOk && catalogText is not null)
        {
            // allow compact JSON without space after colon
            catalogOk = catalogText.Contains($"\"gameId\":\"{adapter.GameId}\"", StringComparison.OrdinalIgnoreCase);
        }

        Check(
            "catalog",
            "Catalog row in samples/fork/catalog.json",
            catalogOk,
            catalogOk ? "found gameId" : $"missing gameId '{adapter.GameId}' in catalog");

        var dockerfileAbs = Path.Combine(repoRoot, adapter.Dockerfile.Replace('/', Path.DirectorySeparatorChar));
        var dockerOk = File.Exists(dockerfileAbs);
        Check(
            "dockerfile",
            "Dockerfile exists at manifest path",
            dockerOk,
            dockerOk ? adapter.Dockerfile : $"missing {adapter.Dockerfile}");

        var buildScript = Path.Combine(repoRoot, "scripts", "build-fork-images.ps1");
        var buildText = File.Exists(buildScript) ? File.ReadAllText(buildScript) : "";
        var buildOk = buildText.Contains($"\"{adapter.BuildImageKey}\"", StringComparison.OrdinalIgnoreCase)
                      || buildText.Contains($"'{adapter.BuildImageKey}'", StringComparison.OrdinalIgnoreCase);
        Check(
            "build-script",
            "Image registered in scripts/build-fork-images.ps1",
            buildOk,
            buildOk ? adapter.BuildImageKey : $"build key '{adapter.BuildImageKey}' not in build-fork-images.ps1");

        var integrationAbs = Path.Combine(repoRoot, adapter.IntegrationDir.Replace('/', Path.DirectorySeparatorChar));
        var integrationOk = Directory.Exists(integrationAbs)
                            && File.Exists(Path.Combine(integrationAbs, "adapter.manifest.json"));
        Check(
            "integration",
            "Runtime / plugin folder integrations/<game>/ with adapter.manifest.json",
            integrationOk,
            integrationOk ? adapter.IntegrationDir : $"missing {adapter.IntegrationDir}/adapter.manifest.json");

        var nleRel = adapter.DefaultNleTemplate.Replace('\\', '/');
        if (!nleRel.StartsWith("samples/", StringComparison.OrdinalIgnoreCase)
            && !nleRel.StartsWith("configs/", StringComparison.OrdinalIgnoreCase))
        {
            nleRel = "samples/" + nleRel.TrimStart('/');
        }
        else if (nleRel.StartsWith("configs/", StringComparison.OrdinalIgnoreCase))
        {
            nleRel = "samples/" + nleRel;
        }

        var nleAbs = Path.Combine(repoRoot, nleRel.Replace('/', Path.DirectorySeparatorChar));
        var nleOk = File.Exists(nleAbs);
        Check(
            "nle",
            ".nle template exists",
            nleOk,
            nleOk ? nleRel : $"missing {nleRel}");

        if (nleOk)
        {
            var nleText = File.ReadAllText(nleAbs);
            var missingEvents = adapter.RequiredEvents
                .Where(e => !nleText.Contains($"event {e}:", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Check(
                "nle-events",
                "Required events appear in .nle template",
                missingEvents.Count == 0,
                missingEvents.Count == 0
                    ? "all required events present"
                    : "missing events: " + string.Join(", ", missingEvents));
        }
        else
        {
            Check("nle-events", "Required events appear in .nle template", false, "skipped (.nle missing)");
        }

        var dogfoodAbs = Path.Combine(repoRoot, adapter.DogfoodScript.Replace('/', Path.DirectorySeparatorChar));
        var dogfoodOk = File.Exists(dogfoodAbs);
        Check(
            "dogfood",
            "Dogfood script exists",
            dogfoodOk,
            dogfoodOk ? adapter.DogfoodScript : $"missing {adapter.DogfoodScript}");

        var sidecar = Path.Combine(integrationAbs, "sidecar", "nl_sidecar.py");
        var readme = Path.Combine(integrationAbs, "README.md");
        var smokeOk = File.Exists(sidecar) || File.Exists(readme)
                      || File.Exists(Path.Combine(integrationAbs, "CHECKLIST.md"));
        Check(
            "smoke-docs",
            "Integration has README/CHECKLIST or sidecar stub",
            smokeOk,
            smokeOk ? "ok" : "add README.md or sidecar/nl_sidecar.py");

        var requiredActionsOk = adapter.RequiredActions.Contains("warn", StringComparer.OrdinalIgnoreCase)
                                && adapter.RequiredActions.Contains("kick", StringComparer.OrdinalIgnoreCase);
        Check(
            "actions",
            "Required actions include warn + kick (Integration Spec v1 minimum)",
            requiredActionsOk,
            requiredActionsOk
                ? string.Join(", ", adapter.RequiredActions)
                : "must include at least warn and kick");

        var connectOk = !string.IsNullOrWhiteSpace(adapter.ConnectScheme);
        Check(
            "connect-scheme",
            "Connect URL scheme documented on adapter",
            connectOk,
            connectOk ? $"{adapter.ConnectScheme}://" : "missing connectScheme");

        var healthOk = !string.IsNullOrWhiteSpace(adapter.Health.Type);
        Check(
            "health",
            "Health / readiness probe configured",
            healthOk,
            healthOk ? $"{adapter.Health.Type}:{adapter.Health.Path}" : "missing health probe");

        if (adapter is ManifestGameForkAdapter manifest && !string.IsNullOrWhiteSpace(manifest.Dto.NativePlugin))
        {
            var pluginRel = manifest.Dto.NativePlugin.Replace('\\', '/');
            var pluginAbs = Path.Combine(repoRoot, pluginRel.Replace('/', Path.DirectorySeparatorChar));
            var about = Path.Combine(pluginAbs, "About", "About.xml");
            var pluginYml = Path.Combine(pluginAbs, "src", "main", "resources", "plugin.yml");
            var pom = Path.Combine(pluginAbs, "pom.xml");
            var pluginOk = Directory.Exists(pluginAbs)
                           && (File.Exists(about) || File.Exists(pluginYml) || File.Exists(pom));
            Check(
                "native-plugin",
                "Native plugin / Harmony mod folder exists",
                pluginOk,
                pluginOk ? pluginRel : $"missing native plugin at {pluginRel}");
        }

        var passed = items.All(i => i.Passed);
        return new GameForkAdapterChecklistReport(adapter.GameId, passed, items);
    }
}
