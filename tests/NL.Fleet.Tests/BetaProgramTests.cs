using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class BetaProgramTests
{
    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-beta-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("NL_FLEET_ROOT", dir);
        NlFleetPaths.EnsureRoot();
        return dir;
    }

    [Fact]
    public void SignUp_Approve_AllowsStreamer()
    {
        var dir = TempRoot();
        try
        {
            var settings = new NlBetaSettings
            {
                Enabled = true,
                WaitlistOpen = true,
                MaxApprovedStreamers = 10,
                EnforceStreamerAllowlist = true,
            };
            var store = new JsonBetaWaitlistStore(Path.Combine(dir, "beta-waitlist.json"));
            var beta = new BetaProgramService(settings, store);

            var entry = beta.SignUp("Alice", "alice@example.com", "aliceplays", "hello-fork");
            Assert.Equal(BetaWaitlistStatus.Pending, entry.Status);

            var denied = beta.CheckStreamer("alice-streamer");
            Assert.False(denied.Allowed);

            var approved = beta.Approve(entry.Id, "alice-streamer");
            Assert.Equal(BetaWaitlistStatus.Approved, approved.Status);

            var allowed = beta.CheckStreamer("alice-streamer");
            Assert.True(allowed.Allowed);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            Environment.SetEnvironmentVariable("NL_FLEET_ROOT", null);
        }
    }

    [Fact]
    public void Validation_PassesWithMockIdentityWhenAllowed()
    {
        var beta = new NlBetaSettings
        {
            Enabled = true,
            WaitlistOpen = true,
            AllowMockIdentity = true,
            RequireProductionReady = false,
            EnforceStreamerAllowlist = true,
        };
        var svc = new BetaValidationService();
        var report = svc.Evaluate(beta, operatorKeyConfigured: true, publicModeEnabled: true, "Mock", steamConfigured: false, productionReady: false);
        Assert.True(report.BetaPassed);
    }
}
