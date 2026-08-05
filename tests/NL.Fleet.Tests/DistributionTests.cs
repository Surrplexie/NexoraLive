using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class DistributionTests
{
    private static DistributionOnboardingPaths Onboarding => new(
        LandingPageEnabled: true,
        DownloadPageEnabled: true,
        WebClientEnabled: true,
        StreamerSignupEnabled: true,
        IdentityLinkEnabled: true,
        CatalogBrowserEnabled: true);

    private static DistributionClientManifestPublic Manifest(bool packageAvailable = true) => new(
        "1.0.0",
        "nlclient",
        "nlclient://join?streamer=demo&game=hello-fork&major=1.0",
        "http://127.0.0.1:27020/nl-client.html",
        "http://127.0.0.1:27020/ga.html",
        "http://127.0.0.1:27020/fork-catalog.html",
        new[]
        {
            new DistributionClientRelease("win-x64", "http://127.0.0.1:27020/downloads/nl-client-win-x64.zip", "abc", packageAvailable),
        },
        DateTimeOffset.UtcNow);

    [Fact]
    public void Validation_PassesInDistributionDevModeWithVerifiedSmokes()
    {
        var distribution = new NlDistributionSettings { Enabled = true, DevMode = true };
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = true };
        var ga = new NlGaSettings { Enabled = true, OpenSignup = true };
        var svc = new DistributionValidationService();

        var report = svc.Evaluate(
            distribution,
            cutover,
            ga,
            Onboarding,
            Manifest(packageAvailable: false),
            productionCutoverPassed: false,
            hostClientPackageVerified: true,
            streamerSignupVerified: true,
            playerJoinVerified: true);

        Assert.True(report.DistributionPassed);
        Assert.Contains(report.Checks, c => c.Id == "distribution_enabled" && c.Passed);
        Assert.Contains(report.Checks, c => c.Id == "cutover_gate" && c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenDistributionDisabled()
    {
        var distribution = new NlDistributionSettings { Enabled = false, DevMode = true };
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = true };
        var ga = new NlGaSettings { Enabled = true, OpenSignup = true };
        var svc = new DistributionValidationService();

        var report = svc.Evaluate(
            distribution,
            cutover,
            ga,
            Onboarding,
            Manifest(),
            true,
            true,
            true,
            true);

        Assert.False(report.DistributionPassed);
        Assert.Contains(report.Checks, c => c.Id == "distribution_enabled" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresCutoverGateWhenDevOff()
    {
        var distribution = new NlDistributionSettings { Enabled = true, DevMode = false, RequireProductionCutover = true };
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = false };
        var ga = new NlGaSettings { Enabled = true, OpenSignup = true };
        var svc = new DistributionValidationService();

        var report = svc.Evaluate(
            distribution,
            cutover,
            ga,
            Onboarding,
            Manifest(),
            productionCutoverPassed: false,
            hostClientPackageVerified: true,
            streamerSignupVerified: true,
            playerJoinVerified: true);

        Assert.False(report.DistributionPassed);
        Assert.Contains(report.Checks, c => c.Id == "cutover_gate" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresClientPackageWhenDevOffAndNotVerified()
    {
        var distribution = new NlDistributionSettings
        {
            Enabled = true,
            DevMode = false,
            RequireClientPackage = true,
        };
        var cutover = new NlProductionCutoverSettings { Enabled = true, DevMode = true };
        var ga = new NlGaSettings { Enabled = true, OpenSignup = true };
        var svc = new DistributionValidationService();

        var report = svc.Evaluate(
            distribution,
            cutover,
            ga,
            Onboarding,
            Manifest(packageAvailable: false),
            productionCutoverPassed: true,
            hostClientPackageVerified: false,
            streamerSignupVerified: true,
            playerJoinVerified: true);

        Assert.False(report.DistributionPassed);
        Assert.Contains(report.Checks, c => c.Id == "client_package" && !c.Passed);
    }
}
