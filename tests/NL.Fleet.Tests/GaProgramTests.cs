using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class GaProgramTests
{
    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-ga-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("NL_FLEET_ROOT", dir);
        NlFleetPaths.EnsureRoot();
        return dir;
    }

    [Fact]
    public void Register_OpenSignup_AllowsAnyStreamer()
    {
        var dir = TempRoot();
        try
        {
            var settings = new NlGaSettings
            {
                Enabled = true,
                OpenSignup = true,
            };
            var store = new JsonGaStreamerStore(Path.Combine(dir, "ga-streamers.json"));
            var ga = new GaProgramService(settings, store);

            var entry = ga.Register("Alice", "alice@example.com", "aliceplays", "minecraft");
            Assert.Equal("minecraft", entry.PreferredGameId);
            Assert.True(ga.IsStreamerAllowed(entry.StreamerId));
            Assert.True(ga.IsStreamerAllowed("any-random-streamer"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Environment.SetEnvironmentVariable("NL_FLEET_ROOT", null);
        }
    }

    [Fact]
    public void CatalogCheck_RequiresGaGames()
    {
        var ga = new NlGaSettings
        {
            MinCatalogGames = 3,
            RequiredGameIds = ["hello-fork", "minecraft", "beamng"],
        };
        var svc = new GaCatalogService();
        var ok = svc.Evaluate(true, ["hello-fork", "minecraft", "beamng", "gameA"], ga);
        Assert.True(ok.Passed);

        var missing = svc.Evaluate(true, ["hello-fork"], ga);
        Assert.False(missing.Passed);
        Assert.Contains("minecraft", missing.MissingGameIds);
    }

    [Fact]
    public void Validation_PassesWithMockIdentityWhenAllowed()
    {
        var ga = new NlGaSettings
        {
            Enabled = true,
            OpenSignup = true,
            AllowMockIdentity = true,
            RequireProductionReady = false,
            RequireBetaDisabled = true,
        };
        var beta = new NlBetaSettings { Enabled = false };
        var catalog = new GaCatalogService().Evaluate(
            true,
            ["hello-fork", "minecraft", "beamng"],
            ga);
        var retention = new FleetModerationRetentionPolicy(730, true, true);
        var svc = new GaValidationService();
        var report = svc.Evaluate(
            ga,
            beta,
            operatorKeyConfigured: true,
            publicModeEnabled: true,
            "Mock",
            steamConfigured: false,
            productionReady: false,
            catalogEnabled: true,
            catalog,
            retention,
            null);
        Assert.True(report.GaPassed);
    }
}
