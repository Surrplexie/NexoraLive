using NL.Fleet;
using NL.Fleet.Core;
using Xunit;

namespace NL.Fleet.Tests;

public class LegalComplianceTests
{
    private static LegalComplianceManifestPublic Manifest => new(
        "2026-08-01",
        13,
        "nl-cookie-consent",
        new[]
        {
            new LegalComplianceDocument("terms", "Terms", "/terms.html", true),
            new LegalComplianceDocument("privacy", "Privacy", "/privacy.html", true),
        },
        new[] { "Steam Web API (Valve)" },
        DateTimeOffset.UtcNow);

    private static LegalComplianceOnboardingPaths Onboarding => new(
        true, true, true, true, true, true, true);

    private static FleetModerationRetentionPolicy Retention => new(730, true, true);

    [Fact]
    public void Validation_PassesInDevModeWithVerifiedSmokes()
    {
        var legal = new NlLegalComplianceSettings { Enabled = true, DevMode = true };
        var launch = new NlLaunchOpsSettings { Enabled = true, LegalVersion = "2026-08-01" };
        var scale = new NlScaleReliabilitySettings { Enabled = true, DevMode = true };
        var svc = new LegalComplianceValidationService();

        var report = svc.Evaluate(
            legal,
            launch,
            scale,
            Retention,
            partnershipEnabled: true,
            Onboarding,
            Manifest,
            auditEntryCount: 0,
            scaleReliabilityPassed: false,
            gdprExportVerified: true,
            streamerTermsVerified: true);

        Assert.True(report.LegalCompliancePassed);
        Assert.Contains(report.Checks, c => c.Id == "scale_reliability_gate" && c.Passed);
    }

    [Fact]
    public void Validation_FailsWhenLegalComplianceDisabled()
    {
        var legal = new NlLegalComplianceSettings { Enabled = false, DevMode = true };
        var launch = new NlLaunchOpsSettings { Enabled = true, LegalVersion = "2026-08-01" };
        var scale = new NlScaleReliabilitySettings { Enabled = true, DevMode = true };
        var svc = new LegalComplianceValidationService();

        var report = svc.Evaluate(
            legal,
            launch,
            scale,
            Retention,
            true,
            Onboarding,
            Manifest,
            1,
            true,
            true,
            true);

        Assert.False(report.LegalCompliancePassed);
        Assert.Contains(report.Checks, c => c.Id == "legal_compliance_enabled" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresScaleGateWhenDevOff()
    {
        var legal = new NlLegalComplianceSettings { Enabled = true, DevMode = false, RequireScaleReliability = true };
        var launch = new NlLaunchOpsSettings { Enabled = true, LegalVersion = "2026-08-01" };
        var scale = new NlScaleReliabilitySettings { Enabled = true, DevMode = false };
        var svc = new LegalComplianceValidationService();

        var report = svc.Evaluate(
            legal,
            launch,
            scale,
            Retention,
            true,
            Onboarding,
            Manifest,
            1,
            scaleReliabilityPassed: false,
            gdprExportVerified: true,
            streamerTermsVerified: true);

        Assert.False(report.LegalCompliancePassed);
        Assert.Contains(report.Checks, c => c.Id == "scale_reliability_gate" && !c.Passed);
    }

    [Fact]
    public void Validation_RequiresAuditLogWhenDevOff()
    {
        var legal = new NlLegalComplianceSettings
        {
            Enabled = true,
            DevMode = false,
            RequireAuditLog = true,
        };
        var launch = new NlLaunchOpsSettings { Enabled = true, LegalVersion = "2026-08-01" };
        var scale = new NlScaleReliabilitySettings { Enabled = true, DevMode = true };
        var svc = new LegalComplianceValidationService();

        var report = svc.Evaluate(
            legal,
            launch,
            scale,
            Retention,
            true,
            Onboarding,
            Manifest,
            auditEntryCount: 0,
            true,
            gdprExportVerified: true,
            streamerTermsVerified: true);

        Assert.False(report.LegalCompliancePassed);
        Assert.Contains(report.Checks, c => c.Id == "compliance_audit" && !c.Passed);
    }
}
