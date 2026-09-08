using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 8 — multi-game production fork images + player UX readiness gate.</summary>
public sealed class MultiGameValidationService
{
    public MultiGameValidationReport Evaluate(
        NlMultiGameProductionSettings multiGame,
        NlLiveProductionSettings liveProduction,
        NlGaSettings ga,
        bool catalogEnabled,
        MultiGameCatalogCheckResult catalog,
        bool liveProductionPassed,
        bool partnershipEnabled,
        bool partnershipGateAtAdmit,
        bool hostImagesVerified,
        IReadOnlyList<string>? verifiedGameIds = null)
    {
        var checks = new List<MultiGameValidationCheck>
        {
            Check("multigame_enabled", "Multi-game production program enabled", multiGame.Enabled),
            Check(
                "live_production",
                "Live production program enabled",
                liveProduction.Enabled),
            Check(
                "live_production_gate",
                "Live production validation gate passed",
                !multiGame.RequireLiveProduction || liveProductionPassed || liveProduction.DevMode,
                liveProduction.DevMode ? "dev mode" : null),
            Check(
                "ga_enabled",
                "General availability enabled",
                ga.Enabled),
            Check(
                "catalog_enabled",
                "Fork catalog enabled",
                catalogEnabled),
            Check(
                "catalog_docker_images",
                "Required games have catalog Docker images",
                catalog.Passed,
                catalog.MissingDockerImages.Count > 0
                    ? $"missing=[{string.Join(", ", catalog.MissingDockerImages)}]"
                    : string.Join("; ", catalog.Games.Select(g => $"{g.GameId}={g.DockerImage}"))),
            Check(
                "host_fork_images",
                "Production fork images verified on host",
                hostImagesVerified,
                verifiedGameIds is { Count: > 0 }
                    ? string.Join(", ", verifiedGameIds)
                    : "run nl-multi-game-validate.ps1 on host with Docker"),
            Check(
                "partnership_gate",
                "Partnership at-own-risk gate enabled",
                !multiGame.RequirePartnershipGate || (partnershipEnabled && partnershipGateAtAdmit),
                partnershipEnabled ? "partnership on" : "NL_PARTNERSHIP_ENABLED=false"),
            Check(
                "player_ux",
                "NL Client player join flow available",
                !multiGame.RequirePlayerUx || multiGame.Enabled,
                "/nl-client.html + /api/v1/client/join-flow"),
        };

        foreach (var game in catalog.Games)
        {
            checks.Add(Check(
                $"game_{game.GameId}_image",
                $"Catalog Docker image for {game.GameId}",
                game.HasDockerImage,
                game.DockerImage));
        }

        var passed = checks.All(c => c.Passed);
        return new MultiGameValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    private static MultiGameValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
