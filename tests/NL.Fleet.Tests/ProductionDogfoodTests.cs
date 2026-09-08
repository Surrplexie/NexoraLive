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
    public void Validation_RequiresRimworldWhenListed()
    {
        var svc = new ProductionDogfoodValidationService();
        var settings = new NlProductionDogfoodSettings
        {
            Enabled = true,
            DevMode = false,
            RequiredGames = ["hello-fork", "rimworld"],
            RequirePlayerJoin = true,
            RequireStreamerSignup = true,
            RequireIdentityAccount = true,
            RequireDockerProvisioner = true,
        };

        var fail = svc.Evaluate(
            settings,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true },
            identityEnabled: true,
            forkOrchestratorEnabled: true,
            orchestratorMode: "Docker",
            streamerSignupVerified: true,
            identityAccountVerified: true,
            playerJoinVerified: true,
            minecraftJoinVerified: false,
            beamngJoinVerified: false,
            forkTeardownVerified: true,
            rimworldJoinVerified: false);

        Assert.False(fail.ProductionDogfoodPassed);
        Assert.Contains(fail.Checks, c => c.Id == "rimworld_join_smoke" && !c.Passed);

        var pass = svc.Evaluate(
            settings,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true },
            identityEnabled: true,
            forkOrchestratorEnabled: true,
            orchestratorMode: "Docker",
            streamerSignupVerified: true,
            identityAccountVerified: true,
            playerJoinVerified: true,
            minecraftJoinVerified: false,
            beamngJoinVerified: false,
            forkTeardownVerified: true,
            rimworldJoinVerified: true);

        Assert.True(pass.ProductionDogfoodPassed);
        Assert.Contains(pass.Checks, c => c.Id == "rimworld_join_smoke" && c.Passed);
        Assert.DoesNotContain(pass.Checks, c => c.Id == "beamng_join_smoke");
    }

    [Fact]
    public void Validation_RequiresKenshiWhenListed()
    {
        var svc = new ProductionDogfoodValidationService();
        var settings = new NlProductionDogfoodSettings
        {
            Enabled = true,
            DevMode = false,
            RequiredGames = ["hello-fork", "kenshi"],
            RequirePlayerJoin = true,
            RequireStreamerSignup = true,
            RequireIdentityAccount = true,
            RequireDockerProvisioner = true,
        };

        var fail = svc.Evaluate(
            settings,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true },
            identityEnabled: true,
            forkOrchestratorEnabled: true,
            orchestratorMode: "Docker",
            streamerSignupVerified: true,
            identityAccountVerified: true,
            playerJoinVerified: true,
            minecraftJoinVerified: false,
            beamngJoinVerified: false,
            forkTeardownVerified: true,
            kenshiJoinVerified: false);

        Assert.False(fail.ProductionDogfoodPassed);
        Assert.Contains(fail.Checks, c => c.Id == "kenshi_join_smoke" && !c.Passed);

        var pass = svc.Evaluate(
            settings,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true },
            identityEnabled: true,
            forkOrchestratorEnabled: true,
            orchestratorMode: "Docker",
            streamerSignupVerified: true,
            identityAccountVerified: true,
            playerJoinVerified: true,
            minecraftJoinVerified: false,
            beamngJoinVerified: false,
            forkTeardownVerified: true,
            kenshiJoinVerified: true);

        Assert.True(pass.ProductionDogfoodPassed);
        Assert.Contains(pass.Checks, c => c.Id == "kenshi_join_smoke" && c.Passed);
        Assert.DoesNotContain(pass.Checks, c => c.Id == "rimworld_join_smoke");
    }

    [Fact]
    public void Validation_PublicLineGamesDoNotRequireBeamng()
    {
        var svc = new ProductionDogfoodValidationService();
        var settings = new NlProductionDogfoodSettings
        {
            Enabled = true,
            DevMode = false,
            RequireMultiGameSmokes = true,
            RequirePlayerJoin = true,
            RequiredGames = ["hello-fork", "minecraft", "rimworld"],
        };

        var report = svc.Evaluate(
            settings,
            Launch,
            new NlGaSettings { Enabled = true, OpenSignup = true },
            new NlDistributionSettings { Enabled = true },
            identityEnabled: true,
            forkOrchestratorEnabled: true,
            orchestratorMode: "Docker",
            streamerSignupVerified: true,
            identityAccountVerified: true,
            playerJoinVerified: true,
            minecraftJoinVerified: true,
            beamngJoinVerified: false,
            forkTeardownVerified: true,
            rimworldJoinVerified: true);

        Assert.True(report.ProductionDogfoodPassed);
        Assert.DoesNotContain(report.Checks, c => c.Id == "beamng_join_smoke");
        Assert.Contains(report.Checks, c => c.Id == "rimworld_join_smoke" && c.Passed);
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
