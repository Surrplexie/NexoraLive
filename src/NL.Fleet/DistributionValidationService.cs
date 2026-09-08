using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 11 — player &amp; streamer distribution readiness gate.</summary>
public sealed class DistributionValidationService
{
    public DistributionValidationReport Evaluate(
        NlDistributionSettings distribution,
        NlProductionCutoverSettings cutover,
        NlGaSettings ga,
        DistributionOnboardingPaths onboarding,
        DistributionClientManifestPublic manifest,
        bool productionCutoverPassed,
        bool hostClientPackageVerified,
        bool streamerSignupVerified,
        bool playerJoinVerified)
    {
        var winRelease = manifest.Releases.FirstOrDefault(r =>
            string.Equals(r.Platform, "win-x64", StringComparison.OrdinalIgnoreCase));
        var packageOk = winRelease?.PackageAvailable == true || hostClientPackageVerified;

        var checks = new List<DistributionValidationCheck>
        {
            Check("distribution_enabled", "Distribution program enabled", distribution.Enabled),
            Check(
                "production_cutover_program",
                "Production cutover program enabled",
                cutover.Enabled),
            Check(
                "cutover_gate",
                "Production cutover validation gate passed",
                !distribution.RequireProductionCutover || productionCutoverPassed || distribution.DevMode),
            Check(
                "landing_page",
                "Public landing page published",
                onboarding.LandingPageEnabled,
                "/play.html"),
            Check(
                "download_page",
                "NL Client download page published",
                onboarding.DownloadPageEnabled,
                "/download.html"),
            Check(
                "client_manifest",
                "Client auto-update manifest available",
                !string.IsNullOrWhiteSpace(manifest.Version),
                $"version={manifest.Version}"),
            Check(
                "client_package",
                "NL Client Windows package on host",
                !distribution.RequireClientPackage || packageOk || distribution.DevMode,
                winRelease?.DownloadUrl),
            Check(
                "deep_link_scheme",
                "nlclient:// deep link scheme documented",
                string.Equals(manifest.DeepLinkScheme, "nlclient", StringComparison.OrdinalIgnoreCase),
                manifest.DeepLinkExample),
            Check(
                "web_player_client",
                "Web NL Client join UI available",
                onboarding.WebClientEnabled,
                manifest.WebClientUrl),
            Check(
                "streamer_onboarding",
                "Open streamer signup enabled",
                onboarding.StreamerSignupEnabled,
                "/ga.html"),
            Check(
                "identity_link",
                "Steam identity link page available",
                onboarding.IdentityLinkEnabled,
                "/identity-link.html"),
            Check(
                "catalog_browser",
                "Fork catalog browser for players",
                onboarding.CatalogBrowserEnabled,
                manifest.CatalogUrl),
            Check(
                "streamer_signup_smoke",
                "Streamer registration smoke verified",
                !distribution.RequireStreamerSignupSmoke || streamerSignupVerified || distribution.DevMode),
            Check(
                "player_join_smoke",
                "Player join flow smoke verified",
                !distribution.RequirePlayerJoinSmoke || playerJoinVerified || distribution.DevMode),
        };

        var passed = checks.All(c => c.Passed);
        return new DistributionValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    private static DistributionValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
