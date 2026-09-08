using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class FleetStagingValidationTests
{
    private static NlFleetHost CreateHost(int maxSessions = 128) =>
        new(new NlFleetSettings
        {
            Enabled = true,
            Autoscale = new FleetAutoscalePolicy(2, maxSessions, true, 15),
            Abuse = new FleetAbusePolicy(6, 0, 1000),
            Relay = new FleetRelayConfig(
                "wss://relay-staging.nl.test/fork/{session}",
                "turn:turn.staging.nl.test:3478"),
        });

    [Fact]
    public void Validation_PassesWith100SessionsAndGoodSlos()
    {
        var host = CreateHost();
        for (var i = 0; i < 120; i++)
        {
            host.Metrics.RecordAdmit(true);
            host.Metrics.RecordForkCreate($"s{i}", "us-east");
            host.Metrics.RecordForkCreateLatency(800 + i);
        }

        var snap = host.Metrics.BuildSnapshot(activeForks: 100, activeNls: 1);
        var load = new FleetLoadTestResult(100, 10, 50, 0, 12.5, 1200, []);
        var report = host.Validation.Evaluate(
            host.Settings,
            "Docker",
            snap,
            host.Metrics,
            host.Incidents,
            load);

        Assert.True(report.StagingPassed);
        Assert.Contains(report.Checks, c => c.Id == "concurrent_sessions_met" && c.Passed);
    }

    [Fact]
    public void SloEvaluator_UsesForkCreateP99FromMetrics()
    {
        var host = CreateHost();
        for (var i = 0; i < 100; i++)
        {
            host.Metrics.RecordForkCreateLatency(i * 10);
        }

        var snap = host.Metrics.BuildSnapshot(10, 1);
        var slos = host.Slo.Evaluate(snap, null, host.Metrics, host.Incidents);
        var p99 = slos.First(s => s.Name == "fork_create_p99_ms");
        Assert.True(p99.Current >= 900);
        Assert.True(p99.Met);
    }

    [Fact]
    public void SloEvaluator_IncidentRestartRateFromStore()
    {
        var host = CreateHost();
        host.Runbook.RecordForkCrash("s1", "st1", autoRestartAttempted: true);
        host.Runbook.RecordForkCrash("s2", "st1", autoRestartAttempted: false);

        var snap = host.Metrics.BuildSnapshot(1, 1);
        var slos = host.Slo.Evaluate(snap, null, host.Metrics, host.Incidents);
        var restart = slos.First(s => s.Name == "incident_auto_restart_rate");
        Assert.Equal(0.5, restart.Current);
        Assert.False(restart.Met);
    }

    [Fact]
    public void ValidationStore_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-fleet-val-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("NL_FLEET_ROOT", dir);
        try
        {
            var host = CreateHost();
            var snap = host.Metrics.BuildSnapshot(100, 1);
            var report = host.Validation.Evaluate(host.Settings, "Docker", snap, host.Metrics, host.Incidents, null);
            host.ValidationStore.Save(report);
            var loaded = host.ValidationStore.GetLast();
            Assert.NotNull(loaded);
            Assert.Equal(report.StagingPassed, loaded!.StagingPassed);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Validation_ProductionGate_AcceptsDockerOrchestrator()
    {
        Environment.SetEnvironmentVariable("NL_FLEET_PRODUCTION_READY", "true");
        Environment.SetEnvironmentVariable("NL_FLEET_STAGING_DEV", "false");
        try
        {
            var host = CreateHost();
            var snap = host.Metrics.BuildSnapshot(activeForks: 100, activeNls: 1);
            var load = new FleetLoadTestResult(100, 10, 50, 0, 12.5, 800, []);
            var report = host.Validation.Evaluate(
                host.Settings,
                "Docker",
                snap,
                host.Metrics,
                host.Incidents,
                load);

            Assert.True(report.ProductionReady);
            Assert.Contains(report.Checks, c => c.Id == "production_orchestrator" && c.Passed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_FLEET_PRODUCTION_READY", null);
            Environment.SetEnvironmentVariable("NL_FLEET_STAGING_DEV", null);
        }
    }
}
