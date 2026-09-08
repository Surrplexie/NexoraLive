using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 6 — general availability readiness gate.</summary>
public sealed class GaValidationService
{
    public GaValidationReport Evaluate(
        NlGaSettings ga,
        NlBetaSettings beta,
        bool operatorKeyConfigured,
        bool publicModeEnabled,
        string? identityMode,
        bool steamConfigured,
        bool productionReady,
        bool catalogEnabled,
        GaCatalogCheckResult catalog,
        FleetModerationRetentionPolicy retention,
        IReadOnlyList<FleetSloStatus>? productionSlos)
    {
        var liveIdentity = string.Equals(identityMode, "Live", StringComparison.OrdinalIgnoreCase);
        var mockIdentityOk = ga.AllowMockIdentity
            && string.Equals(identityMode, "Mock", StringComparison.OrdinalIgnoreCase);
        var identityOk = !ga.RequireLiveIdentity || liveIdentity || mockIdentityOk;

        var checks = new List<GaValidationCheck>
        {
            Check("ga_enabled", "General availability program enabled", ga.Enabled),
            Check(
                "beta_disabled",
                "Beta waitlist disabled for GA launch",
                !ga.RequireBetaDisabled || !beta.Enabled,
                beta.Enabled ? "NL_BETA_ENABLED=true" : "beta off"),
            Check(
                "operator_key",
                "Operator key configured for hosted GA",
                operatorKeyConfigured,
                operatorKeyConfigured ? "set" : "missing NL_OPERATOR_KEY"),
            Check(
                "public_mode",
                "Public mode enabled (operator auth on writes)",
                publicModeEnabled),
            Check(
                "identity_live",
                "Live Steam identity enabled (or mock allowed for local GA)",
                identityOk,
                $"mode={identityMode ?? "unknown"} steam={steamConfigured}"),
            Check(
                "production_ready",
                "Production fleet gate passed",
                !ga.RequireProductionReady || productionReady,
                ga.RequireProductionReady ? null : "skipped for local GA"),
            Check(
                "catalog_enabled",
                "Multi-game fork catalog enabled",
                catalogEnabled),
            Check(
                "catalog_games",
                "Required GA catalog games active",
                catalog.Passed,
                $"active={catalog.ActiveGameCount} missing=[{string.Join(", ", catalog.MissingGameIds)}]"),
            Check(
                "compliance_gdpr",
                "GDPR export and delete enabled",
                retention.AllowGdprExport && retention.AllowGdprDelete),
            Check(
                "compliance_retention",
                "Moderation retention policy configured (≥730 days)",
                retention.RetentionDays >= 730,
                $"days={retention.RetentionDays}"),
            Check(
                "open_signup",
                "Open streamer signup enabled",
                ga.OpenSignup),
            Check(
                "sla_tier",
                "Production SLA tier configured",
                !string.IsNullOrWhiteSpace(ga.SlaTier),
                ga.SlaTier),
        };

        if (productionSlos is { Count: > 0 })
        {
            var sloMet = productionSlos.All(s => s.Met);
            checks.Add(Check(
                "production_sla",
                "Production SLA targets met (observable)",
                sloMet || ga.AllowMockIdentity,
                string.Join("; ", productionSlos.Select(s => $"{s.Name}={s.Current:F2}/{s.Target:F2} met={s.Met}"))));
        }

        var gaPassed = checks.All(c => c.Passed);
        return new GaValidationReport(gaPassed, checks, productionSlos, DateTimeOffset.UtcNow);
    }

    private static GaValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
