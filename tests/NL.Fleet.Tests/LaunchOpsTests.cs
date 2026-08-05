using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class LaunchOpsTests
{
    [Fact]
    public void BackupCheck_PassesWhenHostVerified()
    {
        var settings = new NlLaunchOpsSettings { BackupRoot = Path.GetTempPath() };
        var svc = new LaunchBackupService();
        var result = svc.Evaluate(settings, hostBackupVerified: true);
        Assert.True(result.Passed);
    }

    [Fact]
    public void BackupCheck_FailsWithoutManifest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-backup-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var settings = new NlLaunchOpsSettings { BackupRoot = dir };
            var svc = new LaunchBackupService();
            var result = svc.Evaluate(settings, hostBackupVerified: false);
            Assert.False(result.Passed);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Status_BuildsOperationalSnapshot()
    {
        var svc = new LaunchStatusService();
        var snap = svc.BuildSnapshot(
            sessionHealthy: true,
            orchestratorEnabled: true,
            activeForks: 2,
            identityEnabled: true,
            identityMode: "Live",
            catalogEnabled: true,
            gaEnabled: true,
            hardeningEnabled: true,
            recentIncidents: 0);
        Assert.Equal("operational", snap.OverallStatus);
        Assert.Contains(snap.Components, c => c.Id == "hardening" && c.Status == "operational");
    }

    [Fact]
    public void Validation_PassesInDevModeWithHardening()
    {
        var launch = new NlLaunchOpsSettings { Enabled = true, DevMode = true };
        var multi = new NlMultiGameProductionSettings { Enabled = true };
        var backup = new LaunchBackupCheckResult(false, "/tmp", null, "no backup");
        var retention = new FleetModerationRetentionPolicy(730, true, true);
        var svc = new LaunchOpsValidationService();
        var report = svc.Evaluate(
            launch,
            multi,
            multiGamePassed: true,
            hardeningEnabled: true,
            new FleetAbusePolicy(6, 50, 30),
            retention,
            backup,
            legalPagesVerified: true,
            alertingConfigured: false,
            alertingTestPassed: false);
        Assert.True(report.LaunchOpsPassed);
    }

    [Fact]
    public void Validation_FailsWithoutHardeningWhenRequired()
    {
        var launch = new NlLaunchOpsSettings { Enabled = true, DevMode = false, RequireHardening = true };
        var multi = new NlMultiGameProductionSettings { Enabled = true };
        var backup = new LaunchBackupCheckResult(true, "/data/backups", DateTimeOffset.UtcNow, "ok");
        var retention = new FleetModerationRetentionPolicy(730, true, true);
        var svc = new LaunchOpsValidationService();
        var report = svc.Evaluate(
            launch,
            multi,
            multiGamePassed: true,
            hardeningEnabled: false,
            new FleetAbusePolicy(6, 50, 30),
            retention,
            backup,
            legalPagesVerified: true,
            alertingConfigured: true,
            alertingTestPassed: false);
        Assert.False(report.LaunchOpsPassed);
        Assert.Contains(report.Checks, c => c.Id == "hardening" && !c.Passed);
    }
}
