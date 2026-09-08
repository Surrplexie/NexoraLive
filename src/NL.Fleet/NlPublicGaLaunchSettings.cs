namespace NL.Fleet;

public sealed class NlPublicGaLaunchSettings
{
    public const string EnabledVariable = "NL_PUBLIC_GA_LAUNCH_ENABLED";

    public bool Enabled { get; init; }

    public bool DevMode { get; init; }

    public bool RequireLegalCompliance { get; init; } = true;

    public bool RequireOperatorSignoff { get; init; } = true;

    public bool RequireRecentBackup { get; init; } = true;

    public bool RequireSupportContact { get; init; } = true;

    public string LaunchVersion { get; init; } = "2026-08-01";

    public string? SupportContact { get; init; }

    public static NlPublicGaLaunchSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_PUBLIC_GA_LAUNCH_DEV"));

        var requireLegal = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PUBLIC_GA_LAUNCH_REQUIRE_LEGAL"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireSignoff = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PUBLIC_GA_LAUNCH_REQUIRE_SIGNOFF"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireBackup = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PUBLIC_GA_LAUNCH_REQUIRE_BACKUP"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireSupport = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PUBLIC_GA_LAUNCH_REQUIRE_SUPPORT"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        return new NlPublicGaLaunchSettings
        {
            Enabled = enabled,
            DevMode = devMode,
            RequireLegalCompliance = requireLegal,
            RequireOperatorSignoff = requireSignoff,
            RequireRecentBackup = requireBackup,
            RequireSupportContact = requireSupport,
            LaunchVersion = Environment.GetEnvironmentVariable("NL_PUBLIC_GA_LAUNCH_VERSION")
                ?? Environment.GetEnvironmentVariable("NL_LAUNCH_LEGAL_VERSION")
                ?? "2026-08-01",
            SupportContact = Environment.GetEnvironmentVariable("NL_PUBLIC_GA_SUPPORT_CONTACT"),
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        launchVersion = LaunchVersion,
        supportContact = SupportContact,
        checklistPath = "/ga-launch-checklist.html",
        opsPath = "/public-ga-launch-ops.html",
        validationPath = "/api/v1/public-ga-launch/validation",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
