using System.Net.Http.Json;
using System.Text.Json;
using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Phase 9 — incident alerting via webhook.</summary>
public sealed class LaunchAlertingService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _alertLogPath;

    public LaunchAlertingService(string? alertLogPath = null)
    {
        _alertLogPath = alertLogPath ?? Path.Combine(NlFleetPaths.Root, "launch-alerts.jsonl");
    }

    public bool IsConfigured(NlLaunchOpsSettings settings) =>
        !string.IsNullOrWhiteSpace(settings.AlertWebhookUrl);

    public async Task<bool> SendTestAlertAsync(NlLaunchOpsSettings settings, CancellationToken ct = default)
    {
        if (!IsConfigured(settings))
        {
            return false;
        }

        var payload = new
        {
            source = "nl-launch-ops",
            severity = "info",
            title = "NL launch ops test alert",
            message = "Validation smoke test — alerting channel reachable.",
            observedAtUtc = DateTimeOffset.UtcNow,
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await http.PostAsJsonAsync(settings.AlertWebhookUrl!, payload, ct).ConfigureAwait(false);
        var ok = response.IsSuccessStatusCode;
        RecordAlert("test", ok, ok ? null : $"HTTP {(int)response.StatusCode}");
        return ok;
    }

    public void RecordAlert(string kind, bool success, string? detail)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_alertLogPath)!);
        var entry = new
        {
            kind,
            success,
            detail,
            atUtc = DateTimeOffset.UtcNow,
        };
        File.AppendAllText(_alertLogPath, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
    }
}
