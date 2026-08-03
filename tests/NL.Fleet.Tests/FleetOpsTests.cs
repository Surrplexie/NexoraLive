using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class FleetOpsTests
{
    private static NlFleetHost CreateHost(int minFollowers = 0, int forkPerMin = 100)
    {
        var root = Path.Combine(Path.GetTempPath(), "nl-fleet-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("NL_FLEET_ROOT", root);
        var settings = new NlFleetSettings
        {
            Enabled = true,
            Abuse = new FleetAbusePolicy(6, minFollowers, forkPerMin),
            Autoscale = new FleetAutoscalePolicy(2, 128, true, 15),
            Retention = new FleetModerationRetentionPolicy(730, true, true),
            Relay = new FleetRelayConfig("wss://relay-{region}.test/fork/{session}", "turn:turn.test:3478"),
        };
        return new NlFleetHost(settings);
    }

    [Fact]
    public void RegionPlacement_UsesPreferredRegion()
    {
        var host = CreateHost();
        var result = host.Regions.Place(
            new FleetPlacementRequest("streamer1", PreferredRegion: "eu-west"),
            "http://localhost:8080");
        Assert.Equal("eu-west", result.RegionId);
        Assert.True(result.UsedPreferredRegion);
    }

    [Fact]
    public void RelayMasking_ReplacesRawEndpoint()
    {
        var host = CreateHost();
        var masked = host.Relay.MaskEndpoint("ws://192.168.1.50:7777", "us-east", "sess123");
        Assert.Contains("us-east", masked.PublicConnectUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sess123", masked.PublicConnectUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ws://192.168.1.50:7777", masked.RawEndpoint);
    }

    [Fact]
    public void AbuseGate_BlocksLowFollowers()
    {
        var host = CreateHost(minFollowers: 50);
        var denied = host.Abuse.CheckForkCreate("streamer1", twitchFollowers: 10);
        Assert.False(denied.Allowed);
        Assert.Contains("50", denied.DenyReason, StringComparison.Ordinal);
    }

    [Fact]
    public void AbuseGate_AllowsWhenFollowersMet()
    {
        var host = CreateHost(minFollowers: 50);
        var allowed = host.Abuse.CheckForkCreate("streamer1", twitchFollowers: 100);
        Assert.True(allowed.Allowed);
    }

    [Fact]
    public void Metrics_RecordAdmitAndForkCreate()
    {
        var host = CreateHost();
        host.Metrics.RecordAdmit(true, "s1");
        host.Metrics.RecordAdmit(false, "s1");
        host.Metrics.RecordForkCreate("s1", "us-east");
        var snap = host.Metrics.BuildSnapshot(activeForks: 1, activeNls: 1);
        Assert.Equal(2, snap.TotalAdmits);
        Assert.Equal(1, snap.TotalAdmitDenials);
        Assert.Equal(1, snap.ForkCreateRateLastMinute);
    }

    [Fact]
    public void SloEvaluator_ConcurrentSessionsMetAt100()
    {
        var host = CreateHost();
        var snap = new FleetObservabilitySnapshot(100, 50, 1000, 5, 500, 10, [], DateTimeOffset.UtcNow);
        var slos = host.Slo.Evaluate(snap, null, host.Metrics, host.Incidents);
        var concurrent = slos.First(s => s.Name == "concurrent_ephemeral_sessions");
        Assert.True(concurrent.Met);
        Assert.Equal(100, concurrent.Current);
    }

    [Fact]
    public void Autoscale_ScaleToZeroWhenIdle()
    {
        var host = CreateHost();
        var idle = DateTimeOffset.UtcNow.AddMinutes(-20);
        var warm = host.Autoscale.Evaluate(0, anyLiveStreams: false, idle);
        Assert.True(warm.ScaleToZeroEligible);
        Assert.Equal(0, warm.TargetWarm);
    }

    [Fact]
    public void IncidentRunbook_RecordsForkCrash()
    {
        var host = CreateHost();
        var incident = host.Runbook.RecordForkCrash("fork1", "streamer1", autoRestartAttempted: true);
        Assert.Equal(FleetIncidentKind.ForkCrash, incident.Kind);
        Assert.Single(host.Incidents.ListRecent());
    }
}
