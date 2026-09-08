namespace NL.Fleet;

public sealed class NlLegalComplianceSettings
{
    public const string EnabledVariable = "NL_LEGAL_COMPLIANCE_ENABLED";

    public bool Enabled { get; init; }

    public bool DevMode { get; init; }

    public bool RequireScaleReliability { get; init; } = true;

    public bool RequireGdprSmoke { get; init; } = true;

    public bool RequireStreamerTerms { get; init; } = true;

    public bool RequireAuditLog { get; init; } = true;

    public int MinimumAgeYears { get; init; } = 13;

    public static NlLegalComplianceSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_LEGAL_COMPLIANCE_DEV"));

        var requireScale = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LEGAL_COMPLIANCE_REQUIRE_SCALE"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireGdpr = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LEGAL_COMPLIANCE_REQUIRE_GDPR_SMOKE"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireTerms = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LEGAL_COMPLIANCE_REQUIRE_STREAMER_TERMS"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireAudit = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LEGAL_COMPLIANCE_REQUIRE_AUDIT"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var minAge = int.TryParse(
            Environment.GetEnvironmentVariable("NL_LEGAL_COMPLIANCE_MIN_AGE"),
            out var age)
            ? Math.Max(13, age)
            : 13;

        return new NlLegalComplianceSettings
        {
            Enabled = enabled,
            DevMode = devMode,
            RequireScaleReliability = requireScale,
            RequireGdprSmoke = requireGdpr,
            RequireStreamerTerms = requireTerms,
            RequireAuditLog = requireAudit,
            MinimumAgeYears = minAge,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        minimumAgeYears = MinimumAgeYears,
        legalCenterPath = "/legal-center.html",
        manifestPath = "/api/v1/legal-compliance/manifest",
        opsPath = "/legal-compliance-ops.html",
        gdprExportPath = "/api/v1/fleet/compliance/export/{playerId}",
        gdprDeletePath = "/api/v1/fleet/compliance/sp/{playerId}",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
