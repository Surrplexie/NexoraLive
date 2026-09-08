using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class PublicGaLaunchChecklistService
{
    public PublicGaLaunchChecklist Build(NlPublicGaLaunchSettings launch)
    {
        var items = new List<PublicGaLaunchChecklistItem>
        {
            Item("programs", "Programs", "All release programs enabled", "Distribution, scale, legal, cutover, launch ops flags on", true, "docs/NL_PUBLIC_GA_LAUNCH.md"),
            Item("legal_gate", "Legal", "Legal compliance validation passed", "Terms, privacy, GDPR, audit log", true, "docs/NL_LEGAL_COMPLIANCE.md"),
            Item("scale_gate", "Scale", "Scale & reliability validation passed", "128+ sessions, multi-region, production SLOs", true, "docs/NL_SCALE_RELIABILITY.md"),
            Item("distribution", "Distribution", "Client package and onboarding live", "Download page, manifest, streamer signup", true, "docs/NL_DISTRIBUTION.md"),
            Item("public_landing", "Public", "Landing and status pages reachable", "/play.html, /status.html, /download.html", true),
            Item("support", "Operations", "Support contact published", "NL_PUBLIC_GA_SUPPORT_CONTACT set for player/streamer help", true),
            Item("backup", "Operations", "Recent fleet backup verified", "Run launch-ops backup before go-live", true, "docs/NL_LAUNCH_OPS_RUNBOOK.md"),
            Item("alerting", "Operations", "Incident alerting configured", "NL_LAUNCH_ALERT_WEBHOOK_URL on production VPS", false),
            Item("operator_signoff", "Go-live", "Operator launch signoff recorded", "Confirm checklist in public-ga-launch-ops", true),
            Item("announcement", "Go-live", "Launch announcement ready", "Status page, social, streamer comms", false),
        };

        return new PublicGaLaunchChecklist(launch.LaunchVersion, items, DateTimeOffset.UtcNow);
    }

    private static PublicGaLaunchChecklistItem Item(
        string id,
        string category,
        string title,
        string description,
        bool required,
        string? docPath = null) =>
        new(id, category, title, description, required, docPath);
}
