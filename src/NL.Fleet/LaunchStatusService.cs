using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 9 — public status page component health.</summary>
public sealed class LaunchStatusService
{
    public LaunchStatusPageSnapshot BuildSnapshot(
        bool sessionHealthy,
        bool orchestratorEnabled,
        int activeForks,
        bool identityEnabled,
        string? identityMode,
        bool catalogEnabled,
        bool gaEnabled,
        bool hardeningEnabled,
        int recentIncidents)
    {
        var components = new List<LaunchStatusComponent>
        {
            Component("api", "Session API", sessionHealthy ? "operational" : "outage"),
            Component(
                "identity",
                "Steam identity",
                !identityEnabled ? "outage" : "operational",
                identityMode),
            Component(
                "orchestrator",
                "Fork orchestrator",
                !orchestratorEnabled ? "degraded" : activeForks >= 0 ? "operational" : "degraded",
                orchestratorEnabled ? $"activeForks={activeForks}" : "disabled"),
            Component(
                "catalog",
                "Fork catalog",
                catalogEnabled ? "operational" : "degraded"),
            Component(
                "ga",
                "Streamer signup",
                gaEnabled ? "operational" : "degraded"),
            Component(
                "hardening",
                "Abuse hardening",
                hardeningEnabled ? "operational" : "degraded",
                hardeningEnabled ? "rate limits active" : "NL_HARDENING=false"),
            Component(
                "incidents",
                "Recent incidents",
                recentIncidents == 0 ? "operational" : recentIncidents <= 3 ? "degraded" : "outage",
                $"last24h={recentIncidents}"),
        };

        var overall = components.Any(c => c.Status == "outage")
            ? "outage"
            : components.Any(c => c.Status == "degraded")
                ? "degraded"
                : "operational";

        return new LaunchStatusPageSnapshot(overall, components, DateTimeOffset.UtcNow);
    }

    private static LaunchStatusComponent Component(string id, string name, string status, string? detail = null) =>
        new(id, name, status, detail);
}
