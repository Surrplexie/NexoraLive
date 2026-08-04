using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 7 — live internet production deploy readiness gate.</summary>
public sealed class LiveProductionValidationService
{
    public LiveProductionValidationReport Evaluate(
        NlLiveProductionSettings live,
        NlGaSettings ga,
        NlBetaSettings beta,
        bool operatorKeyConfigured,
        bool publicModeEnabled,
        string? identityMode,
        bool steamConfigured,
        bool productionReady,
        string? publicBaseUrl,
        string relayTemplate,
        string? turnUri,
        bool catalogEnabled,
        GaCatalogCheckResult catalog,
        FleetModerationRetentionPolicy retention)
    {
        var liveIdentity = string.Equals(identityMode, "Live", StringComparison.OrdinalIgnoreCase);
        var dev = live.DevMode;
        var publicUrlOk = IsProductionPublicUrl(publicBaseUrl, dev);
        var relayOk = IsProductionRelayTemplate(relayTemplate, dev);
        var turnOk = !string.IsNullOrWhiteSpace(turnUri);
        var mockDisabled = !ga.AllowMockIdentity || dev;

        var checks = new List<LiveProductionValidationCheck>
        {
            Check("live_production_enabled", "Live production program enabled", live.Enabled),
            Check("ga_enabled", "General availability enabled on live host", ga.Enabled),
            Check(
                "beta_disabled",
                "Beta waitlist disabled",
                !beta.Enabled,
                beta.Enabled ? "NL_BETA_ENABLED=true" : "beta off"),
            Check(
                "operator_key",
                "Operator key configured",
                operatorKeyConfigured,
                operatorKeyConfigured ? "set" : "missing NL_OPERATOR_KEY"),
            Check(
                "public_mode",
                "Public mode enabled",
                publicModeEnabled),
            Check(
                "steam_api_key",
                "Steam Web API key configured",
                steamConfigured,
                steamConfigured ? "set" : "missing STEAM_WEB_API_KEY"),
            Check(
                "identity_live",
                "Live Steam identity mode active",
                liveIdentity || (dev && ga.AllowMockIdentity),
                $"mode={identityMode ?? "unknown"} dev={dev}"),
            Check(
                "mock_identity_disabled",
                "Mock identity disabled for production",
                mockDisabled,
                ga.AllowMockIdentity ? "NL_GA_ALLOW_MOCK_IDENTITY=true" : "mock off"),
            Check(
                "production_ready",
                "Production fleet gate passed",
                productionReady || dev,
                dev ? "skipped in dev mode" : null),
            Check(
                "public_https_url",
                "Public base URL is HTTPS (not localhost)",
                publicUrlOk,
                publicBaseUrl ?? "missing NL_PUBLIC_BASE_URL"),
            Check(
                "relay_production",
                "Relay WebSocket template uses production host",
                relayOk,
                relayTemplate),
            Check(
                "turn_configured",
                "TURN URI configured for NAT traversal",
                turnOk,
                turnUri ?? "missing NL_FLEET_TURN_URI"),
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
                "open_signup",
                "Open streamer signup enabled",
                ga.OpenSignup),
        };

        if (!dev)
        {
            checks.Add(Check(
                "ga_require_live_identity",
                "GA requires live identity (NL_GA_REQUIRE_LIVE_IDENTITY=true)",
                ga.RequireLiveIdentity));
            checks.Add(Check(
                "ga_require_production",
                "GA requires production ready (NL_GA_REQUIRE_PRODUCTION_READY=true)",
                ga.RequireProductionReady && productionReady));
        }

        var passed = checks.All(c => c.Passed);
        return new LiveProductionValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    public static bool IsProductionPublicUrl(string? url, bool devMode)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (devMode)
        {
            return true;
        }

        var host = uri.Host.ToLowerInvariant();
        return host is not ("127.0.0.1" or "localhost" or "::1");
    }

    public static bool IsProductionRelayTemplate(string template, bool devMode)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        if (template.Contains("example.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (devMode)
        {
            return template.StartsWith("wss://", StringComparison.OrdinalIgnoreCase);
        }

        var lower = template.ToLowerInvariant();
        return !lower.Contains("127.0.0.1") && !lower.Contains("localhost");
    }

    private static LiveProductionValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
