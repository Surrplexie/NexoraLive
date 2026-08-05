using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 9 — launch ops & trust readiness gate.</summary>
public sealed class LaunchOpsValidationService
{
    public LaunchOpsValidationReport Evaluate(
        NlLaunchOpsSettings launch,
        NlMultiGameProductionSettings multiGame,
        bool multiGamePassed,
        bool hardeningEnabled,
        FleetAbusePolicy abuse,
        FleetModerationRetentionPolicy retention,
        LaunchBackupCheckResult backup,
        bool legalPagesVerified,
        bool alertingConfigured,
        bool alertingTestPassed)
    {
        var checks = new List<LaunchOpsValidationCheck>
        {
            Check("launch_ops_enabled", "Launch ops program enabled", launch.Enabled),
            Check(
                "multigame_program",
                "Multi-game production enabled",
                multiGame.Enabled),
            Check(
                "multigame_gate",
                "Multi-game validation gate passed",
                !launch.RequireMultiGame || multiGamePassed || launch.DevMode,
                launch.DevMode ? "dev mode" : null),
            Check(
                "hardening",
                "Public abuse hardening enabled (NL_HARDENING)",
                !launch.RequireHardening || hardeningEnabled,
                hardeningEnabled ? "rate limits + WS caps" : "NL_HARDENING=false"),
            Check(
                "fleet_abuse_policy",
                "Fleet fork-create abuse limits configured",
                abuse.MaxForkCreatesPerStreamerPerHour > 0 && abuse.GlobalForkCreatesPerMinute > 0,
                $"perHour={abuse.MaxForkCreatesPerStreamerPerHour} perMin={abuse.GlobalForkCreatesPerMinute}"),
            Check(
                "status_page",
                "Public status page enabled",
                !launch.RequireStatusPage || launch.StatusPageEnabled,
                "/status.html + /api/v1/launch-ops/status"),
            Check(
                "legal_pages",
                "Terms and privacy pages published",
                !launch.RequireLegalPages || legalPagesVerified || launch.DevMode,
                $"version={launch.LegalVersion}"),
            Check(
                "legal_version",
                "Legal document version configured",
                !string.IsNullOrWhiteSpace(launch.LegalVersion)),
            Check(
                "alerting",
                "Incident alerting configured",
                !launch.RequireAlerting || alertingConfigured || alertingTestPassed || launch.DevMode,
                alertingConfigured ? "webhook set" : "NL_LAUNCH_ALERT_WEBHOOK_URL unset"),
            Check(
                "backup",
                "Fleet data backup verified",
                !launch.RequireBackup || backup.Passed || launch.DevMode,
                backup.Detail ?? backup.BackupRoot),
            Check(
                "gdpr_compliance",
                "GDPR export and delete enabled",
                retention.AllowGdprExport && retention.AllowGdprDelete),
            Check(
                "retention_policy",
                "Moderation retention ≥730 days",
                retention.RetentionDays >= 730,
                $"{retention.RetentionDays} days"),
        };

        var passed = checks.All(c => c.Passed);
        return new LaunchOpsValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    private static LaunchOpsValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
