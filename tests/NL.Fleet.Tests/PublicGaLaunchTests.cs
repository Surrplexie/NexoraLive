using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class PublicGaLaunchTests
{
    private static NlPublicGaLaunchSettings Launch => new()
    {
        Enabled = true,
        DevMode = true,
        LaunchVersion = "2026-08-01",
        SupportContact = "support@example.com",
    };

    private static NlGaSettings Ga => new() { Enabled = true, OpenSignup = true };

    [Fact]
    public void Validation_PassesInDevModeWithSignoff()
    {
        var svc = new PublicGaLaunchValidationService();
        var report = svc.Evaluate(
            Launch,
            Ga,
            new NlDistributionSettings { Enabled = true },
            new NlScaleReliabilitySettings { Enabled = true },
            new NlLegalComplianceSettings { Enabled = true },
            new NlLaunchOpsSettings { Enabled = true, StatusPageEnabled = true },
            new NlProductionCutoverSettings { Enabled = true },
            legalCompliancePassed: false,
            backupVerified: true,
            operatorSignoffVerified: true,
            supportContactVerified: true,
            launchAnnouncementReady: false,
            signoffCount: 1);

        Assert.True(report.PublicGaLaunchPassed);
        Assert.Contains(report.Checks, c => c.Id == "legal_compliance_gate" && c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenLaunchDisabled()
    {
        var svc = new PublicGaLaunchValidationService();
        var report = svc.Evaluate(
            new NlPublicGaLaunchSettings { Enabled = false, DevMode = true, LaunchVersion = "2026-08-01", SupportContact = "support@example.com" },
            Ga,
            new NlDistributionSettings { Enabled = true },
            new NlScaleReliabilitySettings { Enabled = true },
            new NlLegalComplianceSettings { Enabled = true },
            new NlLaunchOpsSettings { Enabled = true, StatusPageEnabled = true },
            new NlProductionCutoverSettings { Enabled = true },
            true,
            true,
            true,
            true,
            false,
            1);

        Assert.False(report.PublicGaLaunchPassed);
    }

    [Fact]
    public void Validation_RequiresLegalGateWhenDevOff()
    {
        var svc = new PublicGaLaunchValidationService();
        var report = svc.Evaluate(
            new NlPublicGaLaunchSettings
            {
                Enabled = true,
                DevMode = false,
                RequireLegalCompliance = true,
                LaunchVersion = "2026-08-01",
                SupportContact = "support@example.com",
            },
            Ga,
            new NlDistributionSettings { Enabled = true },
            new NlScaleReliabilitySettings { Enabled = true },
            new NlLegalComplianceSettings { Enabled = true },
            new NlLaunchOpsSettings { Enabled = true, StatusPageEnabled = true },
            new NlProductionCutoverSettings { Enabled = true },
            legalCompliancePassed: false,
            true,
            true,
            true,
            false,
            1);

        Assert.False(report.PublicGaLaunchPassed);
        Assert.Contains(report.Checks, c => c.Id == "legal_compliance_gate" && !c.Passed);
    }

    [Fact]
    public void Checklist_IncludesRequiredItems()
    {
        var checklist = new PublicGaLaunchChecklistService().Build(Launch);
        Assert.Contains(checklist.Items, i => i.Id == "operator_signoff" && i.Required);
        Assert.Contains(checklist.Items, i => i.Id == "legal_gate" && i.Required);
    }
}
