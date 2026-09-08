using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class LiveProductionTests
{
    [Fact]
    public void PublicUrl_ProductionRejectsLocalhost()
    {
        Assert.False(LiveProductionValidationService.IsProductionPublicUrl("https://127.0.0.1", devMode: false));
        Assert.False(LiveProductionValidationService.IsProductionPublicUrl("http://play.example.com", devMode: false));
        Assert.True(LiveProductionValidationService.IsProductionPublicUrl("https://play.example.com", devMode: false));
    }

    [Fact]
    public void PublicUrl_DevModeAllowsLocalhost()
    {
        Assert.True(LiveProductionValidationService.IsProductionPublicUrl("https://127.0.0.1", devMode: true));
    }

    [Fact]
    public void RelayTemplate_ProductionRejectsLocalhost()
    {
        Assert.False(LiveProductionValidationService.IsProductionRelayTemplate(
            "wss://127.0.0.1:8443/fork/{session}", devMode: false));
        Assert.True(LiveProductionValidationService.IsProductionRelayTemplate(
            "wss://relay-us-east.yourdomain.com/fork/{session}", devMode: false));
    }

    [Fact]
    public void Validation_PassesInDevModeWithLiveIdentity()
    {
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = true };
        var ga = new NlGaSettings
        {
            Enabled = true,
            OpenSignup = true,
            AllowMockIdentity = true,
            RequireProductionReady = false,
            RequireLiveIdentity = true,
        };
        var beta = new NlBetaSettings { Enabled = false };
        var catalog = new GaCatalogService().Evaluate(
            true,
            ["hello-fork", "minecraft", "beamng"],
            ga);
        var retention = new FleetModerationRetentionPolicy(730, true, true);
        var svc = new LiveProductionValidationService();
        var report = svc.Evaluate(
            live,
            ga,
            beta,
            operatorKeyConfigured: true,
            publicModeEnabled: true,
            "Live",
            steamConfigured: true,
            productionReady: false,
            "https://127.0.0.1",
            "wss://127.0.0.1:8443/fork/{session}",
            "turn:127.0.0.1:3478?transport=udp",
            catalogEnabled: true,
            catalog,
            retention);
        Assert.True(report.LiveProductionPassed);
    }
}
