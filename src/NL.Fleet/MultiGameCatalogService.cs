using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 8 — catalog rows must expose production Docker images per GA game.</summary>
public sealed class MultiGameCatalogService
{
    public MultiGameCatalogCheckResult Evaluate(
        bool catalogEnabled,
        IReadOnlyList<(string GameId, string? DockerImage, string? MajorVersion)> catalogGames,
        NlMultiGameProductionSettings settings)
    {
        var required = settings.RequiredGameIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statuses = new List<MultiGameCatalogEntryStatus>();
        var missing = new List<string>();

        foreach (var gameId in required)
        {
            var row = catalogGames.FirstOrDefault(g =>
                string.Equals(g.GameId, gameId, StringComparison.OrdinalIgnoreCase));
            var image = string.IsNullOrWhiteSpace(row.GameId) ? null : row.DockerImage;
            var major = string.IsNullOrWhiteSpace(row.GameId) ? null : row.MajorVersion;
            var hasImage = !string.IsNullOrWhiteSpace(image);
            statuses.Add(new MultiGameCatalogEntryStatus(gameId, image, major, hasImage));
            if (!hasImage)
            {
                missing.Add(gameId);
            }
        }

        var passed = catalogEnabled && missing.Count == 0;
        return new MultiGameCatalogCheckResult(passed, statuses, missing);
    }
}
