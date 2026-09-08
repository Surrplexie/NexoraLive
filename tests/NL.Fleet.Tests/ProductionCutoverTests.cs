using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class ProductionCutoverTests
{
    [Fact]
    public void Validation_PassesWhenDevFlagsOffInCutoverDevMode()
    {
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = true };
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = false };
        var launch = new NlLaunchOpsSettings { Enabled = true, DevMode = false };
        var ga = new NlGaSettings
        {
            Enabled = true,
            AllowMockIdentity = false,
            RequireLiveIdentity = true,
            RequireProductionReady = true,
        };
        var beta = new NlBetaSettings { Enabled = false };
        var svc = new ProductionCutoverValidationService();
        var report = svc.Evaluate(
            cutover,
            live,
            launch,
            ga,
            beta,
            operatorKeyConfigured: true,
            publicModeEnabled: true,
            identityMode: "Live",
            steamConfigured: true,
            hardeningEnabled: true,
            productionReady: false,
            publicBaseUrl: "https://127.0.0.1",
            relayTemplate: "wss://127.0.0.1:8443/fork/{session}",
            turnUri: "turn:127.0.0.1:3478",
            liveProductionPassed: false,
            multiGamePassed: false,
            launchOpsPassed: false,
            publicHttpsVerified: true);
        Assert.True(report.ProductionCutoverPassed);
        Assert.Contains(report.Checks, c => c.Id == "mock_identity_off" && c.Passed);
        Assert.Contains(report.Checks, c => c.Id == "live_production_dev_off" && c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenLiveProductionDevStillOn()
    {
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = false };
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = true };
        var launch = new NlLaunchOpsSettings { Enabled = true, DevMode = false };
        var ga = new NlGaSettings { Enabled = true, AllowMockIdentity = false, RequireProductionReady = true };
        var beta = new NlBetaSettings { Enabled = false };
        var svc = new ProductionCutoverValidationService();
        var report = svc.Evaluate(
            cutover,
            live,
            launch,
            ga,
            beta,
            true,
            true,
            "Live",
            true,
            true,
            true,
            "https://play.example.com",
            "wss://relay-us-east.example.com/fork/{session}",
            "turn:turn.example.com:3478",
            true,
            true,
            true,
            true);
        Assert.False(report.ProductionCutoverPassed);
        Assert.Contains(report.Checks, c => c.Id == "live_production_dev_off" && !c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenMockIdentityAllowedInProduction()
    {
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = false };
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = false };
        var launch = new NlLaunchOpsSettings { Enabled = true, DevMode = false };
        var ga = new NlGaSettings { Enabled = true, AllowMockIdentity = true, RequireProductionReady = true };
        var beta = new NlBetaSettings { Enabled = false };
        var svc = new ProductionCutoverValidationService();
        var report = svc.Evaluate(
            cutover,
            live,
            launch,
            ga,
            beta,
            true,
            true,
            "Live",
            true,
            true,
            true,
            "https://play.example.com",
            "wss://relay.example.com/fork/{session}",
            "turn:turn.example.com:3478",
            true,
            true,
            true,
            true);
        Assert.False(report.ProductionCutoverPassed);
        Assert.Contains(report.Checks, c => c.Id == "mock_identity_off" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresRealDomainWhenCutoverDevOff()
    {
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = false };
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = false };
        var launch = new NlLaunchOpsSettings { Enabled = true, DevMode = false };
        var ga = new NlGaSettings
        {
            Enabled = true,
            AllowMockIdentity = false,
            RequireLiveIdentity = true,
            RequireProductionReady = true,
        };
        var beta = new NlBetaSettings { Enabled = false };
        var svc = new ProductionCutoverValidationService();
        var report = svc.Evaluate(
            cutover,
            live,
            launch,
            ga,
            beta,
            true,
            true,
            "Live",
            true,
            true,
            true,
            "https://127.0.0.1",
            "wss://127.0.0.1:8443/fork/{session}",
            "turn:127.0.0.1:3478",
            true,
            true,
            true,
            true);
        Assert.False(report.ProductionCutoverPassed);
        Assert.Contains(report.Checks, c => c.Id == "public_https_url" && !c.Passed);
    }
}
