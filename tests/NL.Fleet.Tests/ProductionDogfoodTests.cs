using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class ProductionDogfoodTests
{
    private static NlProductionDogfoodSettings Dogfood => new()
    {
        Enabled = true,
        DevMode = true,
        RequiredGames = ["hello-fork", "minecraft", "beamng"],
        RequireMultiGameSmokes = true,
    };

    private static NlPublicGaLaunchSettings Launch => new()
    {
        Enabled = true,
        DevMode = true,
        LaunchVersion = "2026-08-01",
        SupportContact = "support@example.com",
    };

    [Fact]
    public void Validation_PassesWhenAllSmokesVerified()
    {
        var svc = new ProductionDogfoodValidationService();
        var report = svc.Evaluate(
            Dogfood,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true, DevMode = true },
            identityEnabled: true,
            forkOrchestratorEnabled: true,
            orchestratorMode: "Docker",
            streamerSignupVerified: true,
            identityAccountVerified: true,
            playerJoinVerified: true,
            minecraftJoinVerified: true,
            beamngJoinVerified: true,
            forkTeardownVerified: true);

        Assert.True(report.ProductionDogfoodPassed);
        Assert.Contains(report.Checks, c => c.Id == "docker_provisioner" && c.Passed);
        Assert.Contains(report.Checks, c => c.Id == "minecraft_join_smoke" && c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenDockerProvisionerMissing()
    {
        var svc = new ProductionDogfoodValidationService();
        var settings = new NlProductionDogfoodSettings
        {
            Enabled = true,
            DevMode = false,
            RequireDockerProvisioner = true,
            RequiredGames = ["hello-fork"],
        };

        var report = svc.Evaluate(
            settings,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true },
            true,
            true,
            "Mock",
            true,
            true,
            true,
            true,
            true,
            true);

        Assert.False(report.ProductionDogfoodPassed);
        Assert.Contains(report.Checks, c => c.Id == "docker_provisioner" && !c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenProgramDisabled()
    {
        var svc = new ProductionDogfoodValidationService();
        var settings = new NlProductionDogfoodSettings
        {
            Enabled = false,
            DevMode = true,
            RequiredGames = ["hello-fork"],
        };

        var report = svc.Evaluate(
            settings,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true, DevMode = true },
            true,
            true,
            "Docker",
            true,
            true,
            true,
            true,
            true,
            true);

        Assert.False(report.ProductionDogfoodPassed);
        Assert.Contains(report.Checks, c => c.Id == "production_dogfood_enabled" && !c.Passed);
    }
}
