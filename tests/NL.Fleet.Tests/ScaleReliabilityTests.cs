using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class ScaleReliabilityTests
{
    private static NlFleetSettings Fleet => new()
    {
        Enabled = true,
        Autoscale = new FleetAutoscalePolicy(2, 128, true, 15),
        Relay = new FleetRelayConfig(
            "wss://relay-{region}.example.com/fork/{session}",
            "turn:turn.example.com:3478"),
    };

    private static FleetObservabilitySnapshot Snapshot(int active) => new(
        active,
        0,
        100,
        0,
        100,
        10,
        Array.Empty<FleetSessionMetricSample>(),
        DateTimeOffset.UtcNow);

    private static FleetLoadTestResult LoadTest(int target, int active) => new(
        target,
        10,
        50,
        0,
        30,
        800,
        Array.Empty<FleetSloStatus>());

    [Fact]
    public void Validation_PassesInDevModeWithVerifiedSmokes()
    {
        var scale = new NlScaleReliabilitySettings { Enabled = true, DevMode = true, MinConcurrentSessions = 128 };
        var distribution = new NlDistributionSettings { Enabled = true, DevMode = true };
        var regions = new FleetRegionService().ListRegions();
        var svc = new ScaleReliabilityValidationService();

        var report = svc.Evaluate(
            scale,
            distribution,
            Fleet,
            regions,
            Snapshot(0),
            null,
            new JsonFleetMetricsStore(),
            new JsonFleetIncidentStore(),
            distributionPassed: false,
            loadTestVerified: true,
            multiRegionVerified: true,
            verifiedRegionIds: ["us-east", "us-west", "eu-west"]);

        Assert.True(report.ScaleReliabilityPassed);
        Assert.Contains(report.Checks, c => c.Id == "scale_enabled" && c.Passed);
        Assert.Contains(report.Checks, c => c.Id == "distribution_gate" && c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenScaleDisabled()
    {
        var scale = new NlScaleReliabilitySettings { Enabled = false, DevMode = true };
        var distribution = new NlDistributionSettings { Enabled = true, DevMode = true };
        var regions = new FleetRegionService().ListRegions();
        var svc = new ScaleReliabilityValidationService();

        var report = svc.Evaluate(
            scale,
            distribution,
            Fleet,
            regions,
            Snapshot(128),
            LoadTest(128, 128),
            new JsonFleetMetricsStore(),
            new JsonFleetIncidentStore(),
            true,
            true,
            true,
            ["us-east", "us-west", "eu-west"]);

        Assert.False(report.ScaleReliabilityPassed);
        Assert.Contains(report.Checks, c => c.Id == "scale_enabled" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresDistributionGateWhenDevOff()
    {
        var scale = new NlScaleReliabilitySettings { Enabled = true, DevMode = false, RequireDistribution = true };
        var distribution = new NlDistributionSettings { Enabled = true, DevMode = false };
        var regions = new FleetRegionService().ListRegions();
        var svc = new ScaleReliabilityValidationService();

        var report = svc.Evaluate(
            scale,
            distribution,
            Fleet,
            regions,
            Snapshot(128),
            LoadTest(128, 128),
            new JsonFleetMetricsStore(),
            new JsonFleetIncidentStore(),
            distributionPassed: false,
            loadTestVerified: true,
            multiRegionVerified: true,
            ["us-east", "us-west", "eu-west"]);

        Assert.False(report.ScaleReliabilityPassed);
        Assert.Contains(report.Checks, c => c.Id == "distribution_gate" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresRelayRegionPlaceholder()
    {
        var scale = new NlScaleReliabilitySettings { Enabled = true, DevMode = false };
        var distribution = new NlDistributionSettings { Enabled = true, DevMode = true };
        var fleet = new NlFleetSettings
        {
            Enabled = true,
            Autoscale = new FleetAutoscalePolicy(2, 128, true, 15),
            Relay = new FleetRelayConfig("wss://relay.example.com/fork/{session}", "turn:turn.example.com:3478"),
        };
        var regions = new FleetRegionService().ListRegions();
        var svc = new ScaleReliabilityValidationService();

        var report = svc.Evaluate(
            scale,
            distribution,
            fleet,
            regions,
            Snapshot(128),
            LoadTest(128, 128),
            new JsonFleetMetricsStore(),
            new JsonFleetIncidentStore(),
            true,
            true,
            true,
            ["us-east", "us-west", "eu-west"]);

        Assert.False(report.ScaleReliabilityPassed);
        Assert.Contains(report.Checks, c => c.Id == "relay_region_template" && !c.Passed);
    }
}
