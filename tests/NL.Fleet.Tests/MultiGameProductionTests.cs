using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class MultiGameProductionTests
{
    [Fact]
    public void CatalogCheck_RequiresDockerImagesForAllGames()
    {
        var settings = new NlMultiGameProductionSettings
        {
            RequiredGameIds = ["hello-fork", "minecraft", "beamng"],
        };
        var svc = new MultiGameCatalogService();
        var ok = svc.Evaluate(
            catalogEnabled: true,
            [
                ("hello-fork", "nl-fork-hello:latest", "1.0"),
                ("minecraft", "nl-fork-minecraft:latest", "1.0"),
                ("beamng", "nl-fork-beamng:latest", "1.0"),
            ],
            settings);
        Assert.True(ok.Passed);
        Assert.Empty(ok.MissingDockerImages);

        var missing = svc.Evaluate(
            catalogEnabled: true,
            [
                ("hello-fork", "nl-fork-hello:latest", "1.0"),
                ("minecraft", null, "1.0"),
            ],
            settings);
        Assert.False(missing.Passed);
        Assert.Contains("beamng", missing.MissingDockerImages);
        Assert.Contains("minecraft", missing.MissingDockerImages);
    }

    [Fact]
    public void Validation_PassesWhenHostImagesVerifiedInDevMode()
    {
        var multiGame = new NlMultiGameProductionSettings { Enabled = true };
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = true };
        var ga = new NlGaSettings { Enabled = true };
        var catalog = new MultiGameCatalogService().Evaluate(
            true,
            [
                ("hello-fork", "nl-fork-hello:latest", "1.0"),
                ("minecraft", "nl-fork-minecraft:latest", "1.0"),
                ("beamng", "nl-fork-beamng:latest", "1.0"),
            ],
            multiGame);

        var svc = new MultiGameValidationService();
        var report = svc.Evaluate(
            multiGame,
            live,
            ga,
            catalogEnabled: true,
            catalog,
            liveProductionPassed: true,
            partnershipEnabled: true,
            partnershipGateAtAdmit: true,
            hostImagesVerified: true,
            ["hello-fork", "minecraft", "beamng"]);

        Assert.True(report.MultiGamePassed);
    }

    [Fact]
    public void Validation_FailsWithoutHostImages()
    {
        var multiGame = new NlMultiGameProductionSettings { Enabled = true };
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = true };
        var ga = new NlGaSettings { Enabled = true };
        var catalog = new MultiGameCatalogService().Evaluate(
            true,
            [
                ("hello-fork", "nl-fork-hello:latest", "1.0"),
                ("minecraft", "nl-fork-minecraft:latest", "1.0"),
                ("beamng", "nl-fork-beamng:latest", "1.0"),
            ],
            multiGame);

        var svc = new MultiGameValidationService();
        var report = svc.Evaluate(
            multiGame,
            live,
            ga,
            catalogEnabled: true,
            catalog,
            liveProductionPassed: true,
            partnershipEnabled: true,
            partnershipGateAtAdmit: true,
            hostImagesVerified: false);

        Assert.False(report.MultiGamePassed);
        Assert.Contains(report.Checks, c => c.Id == "host_fork_images" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresPartnershipGateWhenConfigured()
    {
        var multiGame = new NlMultiGameProductionSettings
        {
            Enabled = true,
            RequirePartnershipGate = true,
        };
        var live = new NlLiveProductionSettings { Enabled = true, DevMode = true };
        var ga = new NlGaSettings { Enabled = true };
        var catalog = new MultiGameCatalogService().Evaluate(
            true,
            [
                ("hello-fork", "nl-fork-hello:latest", "1.0"),
                ("minecraft", "nl-fork-minecraft:latest", "1.0"),
                ("beamng", "nl-fork-beamng:latest", "1.0"),
            ],
            multiGame);

        var svc = new MultiGameValidationService();
        var report = svc.Evaluate(
            multiGame,
            live,
            ga,
            catalogEnabled: true,
            catalog,
            liveProductionPassed: true,
            partnershipEnabled: false,
            partnershipGateAtAdmit: false,
            hostImagesVerified: true,
            ["hello-fork", "minecraft", "beamng"]);

        Assert.False(report.MultiGamePassed);
        Assert.Contains(report.Checks, c => c.Id == "partnership_gate" && !c.Passed);
    }
}
