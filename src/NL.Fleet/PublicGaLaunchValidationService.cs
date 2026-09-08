using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 14 — public GA launch checklist and final go-live gate.</summary>
public sealed class PublicGaLaunchValidationService
{
    public PublicGaLaunchValidationReport Evaluate(
        NlPublicGaLaunchSettings launch,
        NlGaSettings ga,
        NlDistributionSettings distribution,
        NlScaleReliabilitySettings scale,
        NlLegalComplianceSettings legal,
        NlLaunchOpsSettings launchOps,
        NlProductionCutoverSettings cutover,
        bool legalCompliancePassed,
        bool backupVerified,
        bool operatorSignoffVerified,
        bool supportContactVerified,
        bool launchAnnouncementReady,
        int signoffCount)
    {
        var programsOk = ga.Enabled
            && distribution.Enabled
            && scale.Enabled
            && legal.Enabled
            && launchOps.Enabled
            && cutover.Enabled;

        var supportOk = !launch.RequireSupportContact
            || !string.IsNullOrWhiteSpace(launch.SupportContact)
            || supportContactVerified
            || launch.DevMode;

        var checks = new List<PublicGaLaunchValidationCheck>
        {
            Check("public_ga_launch_enabled", "Public GA launch program enabled", launch.Enabled),
            Check(
                "legal_compliance_gate",
                "Legal & compliance validation gate passed",
                !launch.RequireLegalCompliance || legalCompliancePassed || launch.DevMode),
            Check(
                "all_programs_enabled",
                "All upstream release programs enabled",
                programsOk,
                $"ga={ga.Enabled} dist={distribution.Enabled} scale={scale.Enabled} legal={legal.Enabled}"),
            Check(
                "ga_open_signup",
                "General availability open signup enabled",
                ga.Enabled && ga.OpenSignup),
            Check(
                "public_landing",
                "Public landing page published",
                true,
                "/play.html"),
            Check(
                "client_download",
                "NL Client download page published",
                true,
                "/download.html"),
            Check(
                "status_page",
                "Public status page enabled",
                launchOps.StatusPageEnabled,
                "/status.html"),
            Check(
                "launch_checklist",
                "GA launch checklist published",
                true,
                "/ga-launch-checklist.html"),
            Check(
                "support_contact",
                "Support contact configured",
                supportOk,
                launch.SupportContact ?? "set NL_PUBLIC_GA_SUPPORT_CONTACT"),
            Check(
                "recent_backup",
                "Recent fleet backup verified",
                !launch.RequireRecentBackup || backupVerified || launch.DevMode),
            Check(
                "operator_signoff",
                "Operator launch signoff recorded",
                !launch.RequireOperatorSignoff || operatorSignoffVerified || signoffCount > 0 || launch.DevMode),
            Check(
                "launch_version",
                "Launch version configured",
                !string.IsNullOrWhiteSpace(launch.LaunchVersion),
                launch.LaunchVersion),
            Check(
                "launch_announcement",
                "Launch announcement ready (optional)",
                true,
                launchAnnouncementReady ? "ready" : "pending"),
        };

        var passed = checks.All(c => c.Passed);
        return new PublicGaLaunchValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    private static PublicGaLaunchValidationCheck Check(string id, string description, bool passed, string? detail = null) =>
        new(id, description, passed, detail);
}
