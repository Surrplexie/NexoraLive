using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class LegalComplianceValidationService
{
    public LegalComplianceValidationReport Evaluate(
        NlLegalComplianceSettings legal,
        NlLaunchOpsSettings launch,
        NlScaleReliabilitySettings scale,
        FleetModerationRetentionPolicy retention,
        bool partnershipEnabled,
        LegalComplianceOnboardingPaths onboarding,
        LegalComplianceManifestPublic manifest,
        int auditEntryCount,
        bool scaleReliabilityPassed,
        bool gdprExportVerified,
        bool streamerTermsVerified)
    {
        var checks = new List<LegalComplianceValidationCheck>
        {
            Check("legal_compliance_enabled", "Legal & compliance program enabled", legal.Enabled),
            Check(
                "scale_reliability_gate",
                "Scale & reliability validation gate passed",
                !legal.RequireScaleReliability || scaleReliabilityPassed || legal.DevMode),
            Check(
                "launch_ops_legal",
                "Launch ops legal version configured",
                launch.Enabled && !string.IsNullOrWhiteSpace(launch.LegalVersion),
                launch.LegalVersion),
            Check(
                "terms_page",
                "Terms of Service published",
                onboarding.TermsPageEnabled,
                "/terms.html"),
            Check(
                "privacy_page",
                "Privacy Policy published",
                onboarding.PrivacyPageEnabled,
                "/privacy.html"),
            Check(
                "legal_center",
                "Legal center hub published",
                onboarding.LegalCenterEnabled,
                "/legal-center.html"),
            Check(
                "cookie_policy",
                "Cookie policy published",
                onboarding.CookiePolicyEnabled,
                "/cookie-policy.html"),
            Check(
                "subprocessors",
                "Subprocessor list published",
                onboarding.SubprocessorsEnabled && manifest.Subprocessors.Count > 0,
                $"{manifest.Subprocessors.Count} listed"),
            Check(
                "dpa",
                "Data Processing Addendum published",
                onboarding.DpaEnabled,
                "/dpa.html"),
            Check(
                "cookie_consent",
                "Cookie consent banner documented",
                onboarding.CookieConsentBannerEnabled,
                manifest.CookieConsentBannerId),
            Check(
                "minimum_age",
                "Minimum age notice configured",
                manifest.MinimumAgeYears >= 13,
                $"{manifest.MinimumAgeYears}+"),
            Check(
                "gdpr_endpoints",
                "GDPR export and delete enabled",
                retention.AllowGdprExport && retention.AllowGdprDelete),
            Check(
                "retention_policy",
                "Moderation retention at least 730 days",
                retention.RetentionDays >= 730,
                $"{retention.RetentionDays} days"),
            Check(
                "partnership_legal",
                "Partnership legal gate enabled",
                partnershipEnabled),
            Check(
                "gdpr_smoke",
                "GDPR export smoke verified",
                !legal.RequireGdprSmoke || gdprExportVerified || legal.DevMode),
            Check(
                "streamer_terms_smoke",
                "Streamer terms acceptance smoke verified",
                !legal.RequireStreamerTerms || streamerTermsVerified || legal.DevMode),
            Check(
                "compliance_audit",
                "Compliance audit log active",
                !legal.RequireAuditLog || auditEntryCount > 0 || legal.DevMode,
                $"entries={auditEntryCount}"),
        };

        var passed = checks.All(c => c.Passed);
        return new LegalComplianceValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    private static LegalComplianceValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
