using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class LegalComplianceManifestService
{
    public LegalComplianceManifestPublic Build(NlLegalComplianceSettings legal, NlLaunchOpsSettings launch)
    {
        var version = launch.LegalVersion;
        var subprocessors = LoadSubprocessors();

        var documents = new List<LegalComplianceDocument>
        {
            new("terms", "Terms of Service", "/terms.html", true),
            new("privacy", "Privacy Policy", "/privacy.html", true),
            new("legal-center", "Legal Center", "/legal-center.html", true),
            new("cookie-policy", "Cookie Policy", "/cookie-policy.html", true),
            new("subprocessors", "Subprocessors", "/subprocessors.html", true),
            new("dpa", "Data Processing Addendum", "/dpa.html", true),
        };

        return new LegalComplianceManifestPublic(
            version,
            legal.MinimumAgeYears,
            "nl-cookie-consent",
            documents,
            subprocessors,
            DateTimeOffset.UtcNow);
    }

    public LegalComplianceOnboardingPaths BuildOnboardingPaths() => new(
        TermsPageEnabled: true,
        PrivacyPageEnabled: true,
        LegalCenterEnabled: true,
        CookiePolicyEnabled: true,
        SubprocessorsEnabled: true,
        DpaEnabled: true,
        CookieConsentBannerEnabled: true);

    private static IReadOnlyList<string> LoadSubprocessors()
    {
        var raw = Environment.GetEnvironmentVariable("NL_LEGAL_COMPLIANCE_SUBPROCESSORS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return
            [
                "Steam Web API (Valve)",
                "Twitch API (Amazon)",
                "Cloud host / CDN (operator)",
            ];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
