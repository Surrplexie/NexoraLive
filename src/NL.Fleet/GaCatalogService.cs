using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 6 — multi-game catalog readiness for general availability.</summary>
public sealed class GaCatalogService
{
    public GaCatalogCheckResult Evaluate(
        bool catalogEnabled,
        IReadOnlyList<string> activeGameIds,
        NlGaSettings settings)
    {
        var required = settings.RequiredGameIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var minGames = Math.Max(settings.MinCatalogGames, required.Count);
        var active = activeGameIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var presentRequired = required
            .Where(g => active.Any(a => string.Equals(a, g, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var missing = required
            .Where(g => !active.Any(a => string.Equals(a, g, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var passed = catalogEnabled
            && active.Count >= minGames
            && missing.Count == 0;

        return new GaCatalogCheckResult(
            passed,
            active.Count,
            required,
            missing,
            presentRequired);
    }
}
