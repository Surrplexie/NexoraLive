using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 12 — GA scale and reliability: multi-region placement + production load SLOs.</summary>
public sealed class ScaleReliabilityValidationService
{
    public ScaleReliabilityValidationReport Evaluate(
        NlScaleReliabilitySettings scale,
        NlDistributionSettings distribution,
        NlFleetSettings fleet,
        IReadOnlyList<FleetRegion> regions,
        FleetObservabilitySnapshot snapshot,
        FleetLoadTestResult? loadTest,
        IFleetMetricsStore metrics,
        IFleetIncidentStore incidents,
        bool distributionPassed,
        bool loadTestVerified,
        bool multiRegionVerified,
        IReadOnlyList<string> verifiedRegionIds)
    {
        var productionSlos = new FleetSloEvaluator().EvaluateProduction(
            snapshot,
            loadTest,
            metrics,
            incidents);

        var regionIds = regions.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var verifiedSet = verifiedRegionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allRegionsCovered = regionIds.Count >= scale.MinRegions
            && regionIds.All(id => verifiedSet.Contains(id));

        var loadTargetMet = loadTest is not null
            && loadTest.ConcurrentSessionsTarget >= scale.MinConcurrentSessions
            && snapshot.ActiveForkSessions >= Math.Min(scale.MinConcurrentSessions, loadTest.ConcurrentSessionsTarget);

        var relayTemplate = fleet.Relay.RelayWebSocketTemplate;
        var relayHasRegion = relayTemplate.Contains("{region}", StringComparison.OrdinalIgnoreCase);

        var checks = new List<ScaleReliabilityValidationCheck>
        {
            Check("scale_enabled", "Scale & reliability program enabled", scale.Enabled),
            Check(
                "distribution_program",
                "Distribution program enabled",
                distribution.Enabled),
            Check(
                "distribution_gate",
                "Distribution validation gate passed",
                !scale.RequireDistribution || distributionPassed || scale.DevMode),
            Check("fleet_enabled", "Fleet ops enabled", fleet.Enabled),
            Check(
                "multi_region_catalog",
                "Multi-region catalog published",
                regions.Count >= scale.MinRegions,
                $"regions={regions.Count}"),
            Check(
                "max_concurrent_gte_ga",
                "Max concurrent sessions supports GA traffic",
                fleet.Autoscale.MaxConcurrentSessions >= scale.MinConcurrentSessions,
                $"max={fleet.Autoscale.MaxConcurrentSessions} target={scale.MinConcurrentSessions}"),
            Check(
                "relay_region_template",
                "Relay template supports per-region URLs",
                relayHasRegion,
                relayTemplate),
            Check(
                "turn_configured",
                "TURN URI configured for NAT traversal",
                !string.IsNullOrWhiteSpace(fleet.Relay.TurnUri),
                fleet.Relay.TurnUri),
            Check(
                "multi_region_placement",
                "Multi-region fork placement verified",
                !scale.RequireMultiRegion || multiRegionVerified || allRegionsCovered || scale.DevMode,
                string.Join(", ", verifiedSet)),
            Check(
                "ga_load_test",
                "GA traffic load test recorded",
                !scale.RequireLoadTest || loadTargetMet || loadTestVerified || scale.DevMode,
                loadTest is null
                    ? "no load test"
                    : $"target={loadTest.ConcurrentSessionsTarget} active={snapshot.ActiveForkSessions}"),
        };

        foreach (var slo in productionSlos)
        {
            checks.Add(Check(
                $"production_slo_{slo.Name}",
                $"Production SLO: {slo.Name}",
                slo.Met || scale.DevMode || loadTestVerified,
                $"current={slo.Current} target={slo.Target} {slo.Unit}"));
        }

        checks.Add(Check(
            "autoscale_policy",
            "Autoscale warm pool configured",
            fleet.Autoscale.MinWarmSnapshots >= 0 && fleet.Autoscale.ScaleToZeroWhenIdle,
            $"warm={fleet.Autoscale.MinWarmSnapshots} idle={fleet.Autoscale.IdleMinutesBeforeScaleDown}m"));

        var passed = checks.All(c => c.Passed);
        return new ScaleReliabilityValidationReport(passed, checks, productionSlos, DateTimeOffset.UtcNow);
    }

    private static ScaleReliabilityValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
