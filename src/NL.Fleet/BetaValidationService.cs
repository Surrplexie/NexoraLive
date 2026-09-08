using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 5 — public beta readiness gate.</summary>
public sealed class BetaValidationService
{
    public BetaValidationReport Evaluate(
        NlBetaSettings beta,
        bool operatorKeyConfigured,
        bool publicModeEnabled,
        string? identityMode,
        bool steamConfigured,
        bool productionReady)
    {
        var liveIdentity = string.Equals(identityMode, "Live", StringComparison.OrdinalIgnoreCase);
        var mockIdentityOk = beta.AllowMockIdentity
            && string.Equals(identityMode, "Mock", StringComparison.OrdinalIgnoreCase);

        var checks = new List<BetaValidationCheck>
        {
            Check("beta_enabled", "Public beta program enabled", beta.Enabled),
            Check(
                "operator_key",
                "Operator key configured for hosted beta",
                operatorKeyConfigured,
                operatorKeyConfigured ? "set" : "missing NL_OPERATOR_KEY"),
            Check(
                "public_mode",
                "Public mode enabled (operator auth on writes)",
                publicModeEnabled),
            Check(
                "identity_live",
                "Live Steam identity enabled (or mock allowed for local beta)",
                liveIdentity || mockIdentityOk,
                $"mode={identityMode ?? "unknown"} steam={steamConfigured}"),
            Check(
                "steam_api",
                "Steam Web API key configured (or NL_BETA_ALLOW_MOCK_IDENTITY=true)",
                steamConfigured || beta.AllowMockIdentity),
            Check(
                "production_ready",
                "Production fleet gate passed",
                !beta.RequireProductionReady || productionReady,
                beta.RequireProductionReady ? null : "skipped for local beta"),
            Check(
                "waitlist_open",
                "Beta waitlist accepts signups",
                beta.WaitlistOpen),
            Check(
                "streamer_allowlist",
                "Beta streamer allowlist enforcement enabled",
                beta.EnforceStreamerAllowlist),
        };

        var betaPassed = checks.All(c => c.Passed);
        return new BetaValidationReport(betaPassed, checks, DateTimeOffset.UtcNow);
    }

    private static BetaValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
