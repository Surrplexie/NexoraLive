namespace NL.Fleet;

public sealed class NlLaunchOpsSettings
{
    public const string EnabledVariable = "NL_LAUNCH_OPS_ENABLED";

    public bool Enabled { get; init; }

    public bool DevMode { get; init; }

    public bool RequireMultiGame { get; init; } = true;

    public bool RequireHardening { get; init; } = true;

    public bool RequireStatusPage { get; init; } = true;

    public bool RequireLegalPages { get; init; } = true;

    public bool RequireAlerting { get; init; } = true;

    public bool RequireBackup { get; init; } = true;

    public bool StatusPageEnabled { get; init; } = true;

    public string? AlertWebhookUrl { get; init; }

    public string? BackupRoot { get; init; }

    public string LegalVersion { get; init; } = "2026-08-01";

    public int BackupMaxAgeHours { get; init; } = 48;

    public static NlLaunchOpsSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_LAUNCH_OPS_DEV"));

        var requireMulti = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LAUNCH_REQUIRE_MULTIGAME"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireHardening = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LAUNCH_REQUIRE_HARDENING"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireStatus = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LAUNCH_REQUIRE_STATUS_PAGE"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireLegal = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LAUNCH_REQUIRE_LEGAL"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireAlert = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LAUNCH_REQUIRE_ALERTING"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireBackup = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LAUNCH_REQUIRE_BACKUP"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var statusPage = !string.Equals(
            Environment.GetEnvironmentVariable("NL_LAUNCH_STATUS_PAGE_ENABLED"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var maxAge = int.TryParse(Environment.GetEnvironmentVariable("NL_LAUNCH_BACKUP_MAX_AGE_HOURS"), out var hours)
            ? Math.Max(1, hours)
            : 48;

        var backupRoot = Environment.GetEnvironmentVariable("NL_LAUNCH_BACKUP_ROOT");
        if (string.IsNullOrWhiteSpace(backupRoot))
        {
            backupRoot = Path.Combine(NL.Core.NlPaths.Root, "backups");
        }

        return new NlLaunchOpsSettings
        {
            Enabled = enabled,
            DevMode = devMode,
            RequireMultiGame = requireMulti,
            RequireHardening = requireHardening,
            RequireStatusPage = requireStatus,
            RequireLegalPages = requireLegal,
            RequireAlerting = requireAlert,
            RequireBackup = requireBackup,
            StatusPageEnabled = statusPage,
            AlertWebhookUrl = Environment.GetEnvironmentVariable("NL_LAUNCH_ALERT_WEBHOOK_URL"),
            BackupRoot = backupRoot,
            LegalVersion = Environment.GetEnvironmentVariable("NL_LAUNCH_LEGAL_VERSION") ?? "2026-08-01",
            BackupMaxAgeHours = maxAge,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        statusPageEnabled = StatusPageEnabled,
        statusPagePath = "/status.html",
        opsPath = "/launch-ops.html",
        legal = new
        {
            version = LegalVersion,
            termsPath = "/terms.html",
            privacyPath = "/privacy.html",
        },
        backupRoot = BackupRoot,
        alertWebhookConfigured = !string.IsNullOrWhiteSpace(AlertWebhookUrl),
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
