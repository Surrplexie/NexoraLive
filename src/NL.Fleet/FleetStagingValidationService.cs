using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase S — staging → production readiness gate and checklist.</summary>
public sealed class FleetStagingValidationService
{
    public FleetValidationReport Evaluate(
        NlFleetSettings fleet,
        string? orchestratorMode,
        FleetObservabilitySnapshot snapshot,
        IFleetMetricsStore metrics,
        IFleetIncidentStore incidents,
        FleetLoadTestResult? loadTest = null)
    {
        var stagingDev = IsTruthy(Environment.GetEnvironmentVariable("NL_FLEET_STAGING_DEV"));
        var productionGate = IsTruthy(Environment.GetEnvironmentVariable("NL_FLEET_PRODUCTION_READY"));

        var forkP99 = loadTest?.ForkCreateP99Ms > 0
            ? loadTest.ForkCreateP99Ms
            : metrics.GetForkCreateP99Ms();

        var slos = new FleetSloEvaluator().Evaluate(snapshot, loadTest, metrics, incidents);
        var checks = new List<FleetValidationCheck>
        {
            Check("fleet_enabled", "Fleet ops enabled", fleet.Enabled),
            Check(
                "max_concurrent_gte_100",
                "Max concurrent sessions ≥ 100",
                fleet.Autoscale.MaxConcurrentSessions >= 100,
                $"max={fleet.Autoscale.MaxConcurrentSessions}"),
            Check(
                "orchestrator_multi_node",
                "Orchestrator uses Docker/Kubernetes/Process, or load test proved 100+ mock sessions",
                (orchestratorMode is "Docker" or "Kubernetes" or "Process" || stagingDev
                || (loadTest?.ConcurrentSessionsTarget >= 100 && snapshot.ActiveForkSessions >= 100)),
                $"mode={orchestratorMode ?? "unknown"} active={snapshot.ActiveForkSessions}"),
            Check(
                "relay_configured",
                "Relay WebSocket template configured",
                !string.IsNullOrWhiteSpace(fleet.Relay.RelayWebSocketTemplate)),
            Check(
                "relay_production_host",
                "Relay host is not example.com placeholder",
                !fleet.Relay.RelayWebSocketTemplate.Contains("example.com", StringComparison.OrdinalIgnoreCase)
                || stagingDev,
                fleet.Relay.RelayWebSocketTemplate),
            Check(
                "turn_configured",
                "TURN URI configured for NAT traversal",
                !string.IsNullOrWhiteSpace(fleet.Relay.TurnUri)),
            Check(
                "concurrent_sessions_met",
                "100+ concurrent ephemeral fork sessions observed",
                snapshot.ActiveForkSessions >= 100
                || (loadTest?.ConcurrentSessionsTarget >= 100 && loadTest.AdmitsSucceeded + loadTest.AdmitsFailed >= 0
                    && snapshot.ActiveForkSessions >= Math.Min(100, loadTest.ConcurrentSessionsTarget)),
                $"active={snapshot.ActiveForkSessions}"),
            Check(
                "admit_success_slo",
                "Admit success rate SLO met (≥99%)",
                slos.First(s => s.Name == "admit_success_rate").Met),
            Check(
                "fork_create_p99_slo",
                "Fork create p99 latency SLO met (≤5000 ms)",
                slos.First(s => s.Name == "fork_create_p99_ms").Met,
                $"p99={forkP99:F0}ms"),
            Check(
                "incident_restart_slo",
                "Incident auto-restart rate SLO met (≥95%)",
                slos.First(s => s.Name == "incident_auto_restart_rate").Met),
        };

        if (productionGate)
        {
            checks.Add(Check(
                "production_orchestrator",
                "Production requires Docker or Kubernetes provisioner (real container forks)",
                string.Equals(orchestratorMode, "Kubernetes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orchestratorMode, "Docker", StringComparison.OrdinalIgnoreCase),
                $"mode={orchestratorMode}"));
        }

        var stagingPassed = checks
            .Where(c => productionGate || c.Id is not ("relay_production_host" or "production_orchestrator"))
            .All(c => c.Passed);
        var productionReady = checks.All(c => c.Passed);

        return new FleetValidationReport(
            productionReady,
            stagingPassed,
            checks,
            slos,
            loadTest,
            DateTimeOffset.UtcNow);
    }

    private static FleetValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
