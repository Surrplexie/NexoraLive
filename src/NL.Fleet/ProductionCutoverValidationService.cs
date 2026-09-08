using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 10 — production VPS cutover: no dev shortcuts, public HTTPS, upstream gates.</summary>
public sealed class ProductionCutoverValidationService
{
    public ProductionCutoverValidationReport Evaluate(
        NlProductionCutoverSettings cutover,
        NlLiveProductionSettings live,
        NlLaunchOpsSettings launch,
        NlGaSettings ga,
        NlBetaSettings beta,
        bool operatorKeyConfigured,
        bool publicModeEnabled,
        string? identityMode,
        bool steamConfigured,
        bool hardeningEnabled,
        bool productionReady,
        string? publicBaseUrl,
        string relayTemplate,
        string? turnUri,
        bool liveProductionPassed,
        bool multiGamePassed,
        bool launchOpsPassed,
        bool publicHttpsVerified)
    {
        var liveIdentity = string.Equals(identityMode, "Live", StringComparison.OrdinalIgnoreCase);
        var urlDev = cutover.DevMode;
        var publicUrlOk = LiveProductionValidationService.IsProductionPublicUrl(publicBaseUrl, devMode: false);
        var relayOk = LiveProductionValidationService.IsProductionRelayTemplate(relayTemplate, devMode: false);
        var turnOk = !string.IsNullOrWhiteSpace(turnUri);

        var checks = new List<ProductionCutoverValidationCheck>
        {
            Check("cutover_enabled", "Production cutover program enabled", cutover.Enabled),
            Check(
                "live_production_dev_off",
                "Live production dev mode disabled (NL_LIVE_PRODUCTION_DEV=false)",
                !live.DevMode,
                live.DevMode ? "NL_LIVE_PRODUCTION_DEV=true" : "dev off"),
            Check(
                "launch_ops_dev_off",
                "Launch ops dev mode disabled (NL_LAUNCH_OPS_DEV=false)",
                !launch.DevMode,
                launch.DevMode ? "NL_LAUNCH_OPS_DEV=true" : "dev off"),
            Check(
                "mock_identity_off",
                "Mock identity disabled (NL_GA_ALLOW_MOCK_IDENTITY=false)",
                !ga.AllowMockIdentity,
                ga.AllowMockIdentity ? "mock allowed" : "mock off"),
            Check(
                "ga_require_live_identity",
                "GA requires live Steam identity",
                ga.RequireLiveIdentity),
            Check(
                "ga_require_production_ready",
                "GA requires production-ready fleet gate",
                ga.RequireProductionReady),
            Check(
                "identity_live",
                "Live Steam identity mode active",
                liveIdentity,
                $"mode={identityMode ?? "unknown"}"),
            Check(
                "steam_api_key",
                "Steam Web API key configured",
                steamConfigured),
            Check(
                "beta_disabled",
                "Beta waitlist disabled",
                !beta.Enabled),
            Check(
                "operator_key",
                "Operator key configured",
                operatorKeyConfigured),
            Check(
                "public_mode",
                "Public mode enabled",
                publicModeEnabled),
            Check(
                "hardening",
                "Abuse hardening enabled",
                hardeningEnabled),
            Check(
                "public_https_url",
                "Public base URL is HTTPS (not localhost)",
                publicUrlOk || urlDev,
                publicBaseUrl ?? "missing NL_PUBLIC_BASE_URL"),
            Check(
                "relay_production",
                "Relay template uses production host",
                relayOk || urlDev,
                relayTemplate),
            Check(
                "turn_configured",
                "TURN URI configured",
                turnOk,
                turnUri),
            Check(
                "https_edge_probe",
                "HTTPS edge reachable (operator probe)",
                !cutover.RequirePublicHttpsProbe || publicHttpsVerified || urlDev,
                publicHttpsVerified ? "probe ok" : "run cutover validate script"),
            Check(
                "production_ready_runtime",
                "Fleet production-ready gate passed",
                productionReady || urlDev,
                productionReady ? "ready" : "run load test on VPS"),
            Check(
                "live_production_gate",
                "Live production validation gate passed",
                !cutover.RequireLiveProductionGate || liveProductionPassed || urlDev,
                live.DevMode ? "live dev on" : null),
            Check(
                "multigame_gate",
                "Multi-game validation gate passed",
                !cutover.RequireMultiGameGate || multiGamePassed || urlDev),
            Check(
                "launch_ops_gate",
                "Launch ops validation gate passed",
                !cutover.RequireLaunchOpsGate || launchOpsPassed || urlDev),
        };

        var passed = checks.All(c => c.Passed);
        return new ProductionCutoverValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    private static ProductionCutoverValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
