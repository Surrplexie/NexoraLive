using NL.Fleet.Core;

namespace NL.Fleet;

public static class FleetSloCatalog
{
    public static IReadOnlyList<FleetSloDefinition> StagingDefaults { get; } =
    [
        new("concurrent_ephemeral_sessions", 100, "sessions", "Support 100+ concurrent fork sessions in staging."),
        new("admit_success_rate", 0.99, "ratio", "Admit success rate under load."),
        new("fork_create_p99_ms", ResolveForkCreateP99Target(), "ms", "Fork create p99 latency."),
        new("incident_auto_restart_rate", 0.95, "ratio", "Fork crashes auto-restarted within grace window."),
    ];

    private static double ResolveForkCreateP99Target() =>
        int.TryParse(Environment.GetEnvironmentVariable("NL_FLEET_FORK_CREATE_P99_MS"), out var ms)
            ? Math.Max(1000, ms)
            : 5000;
}

public sealed class FleetRegionService
{
    private static readonly IReadOnlyList<FleetRegion> Regions =
    [
        new("us-east", "US East", 10),
        new("us-west", "US West", 40),
        new("eu-west", "EU West", 80),
    ];

    public IReadOnlyList<FleetRegion> ListRegions() => Regions;

    public FleetPlacementResult Place(FleetPlacementRequest request, string defaultOrchestratorBase)
    {
        var region = ResolveRegion(request);
        var relay = Environment.GetEnvironmentVariable("NL_FLEET_RELAY_BASE")
            ?? $"wss://relay-{region.Id}.nl.example.com/fork";
        return new FleetPlacementResult(
            region.Id,
            $"{defaultOrchestratorBase}?region={region.Id}",
            relay,
            string.Equals(region.Id, request.PreferredRegion, StringComparison.OrdinalIgnoreCase));
    }

    private static FleetRegion ResolveRegion(FleetPlacementRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PreferredRegion))
        {
            var preferred = Regions.FirstOrDefault(r =>
                string.Equals(r.Id, request.PreferredRegion, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        var hint = request.StreamerGeoHint?.Trim().ToLowerInvariant();
        if (hint is "eu" or "europe")
        {
            return Regions.First(r => r.Id == "eu-west");
        }

        if (hint is "west" or "us-west")
        {
            return Regions.First(r => r.Id == "us-west");
        }

        return Regions.First(r => r.Id == "us-east");
    }
}

public sealed class FleetRelayService
{
    private readonly FleetRelayConfig _config;

    public FleetRelayService(FleetRelayConfig config) => _config = config;

    public FleetRelayConnectInfo MaskEndpoint(string rawConnectEndpoint, string regionId, string sessionId)
    {
        if (!_config.MaskRawHostIps || string.IsNullOrWhiteSpace(rawConnectEndpoint))
        {
            return new FleetRelayConnectInfo(rawConnectEndpoint, rawConnectEndpoint, regionId);
        }

        var publicUrl = _config.RelayWebSocketTemplate
            .Replace("{region}", regionId, StringComparison.OrdinalIgnoreCase)
            .Replace("{session}", sessionId, StringComparison.OrdinalIgnoreCase);
        return new FleetRelayConnectInfo(publicUrl, rawConnectEndpoint, regionId);
    }
}

public sealed class FleetAbuseGateService
{
    private readonly FleetAbusePolicy _policy;
    private readonly IFleetMetricsStore _metrics;
    private readonly IFleetStreamerRequirementsStore _requirements;

    public FleetAbuseGateService(
        FleetAbusePolicy policy,
        IFleetMetricsStore metrics,
        IFleetStreamerRequirementsStore requirements)
    {
        _policy = policy;
        _metrics = metrics;
        _requirements = requirements;
    }

    public FleetAbuseCheckResult CheckForkCreate(string streamerId, int? twitchFollowers = null)
    {
        if (_metrics.GetForkCreatesInLastMinute() >= _policy.GlobalForkCreatesPerMinute)
        {
            return new FleetAbuseCheckResult(false, "Global fork create rate limit exceeded.");
        }

        if (_metrics.GetForkCreatesForStreamerInLastHour(streamerId) >= _policy.MaxForkCreatesPerStreamerPerHour)
        {
            return new FleetAbuseCheckResult(false, "Streamer fork create hourly quota exceeded.");
        }

        var req = _requirements.GetOrDefault(streamerId);
        if (req.EnforceOnForkCreate)
        {
            var followers = twitchFollowers ?? 0;
            if (followers < req.MinTwitchFollowers)
            {
                return new FleetAbuseCheckResult(
                    false,
                    $"Streamer requires at least {req.MinTwitchFollowers} Twitch followers to host a fork session.");
            }
        }
        else if (_policy.MinTwitchFollowers > 0)
        {
            var followers = twitchFollowers ?? 0;
            if (followers < _policy.MinTwitchFollowers)
            {
                return new FleetAbuseCheckResult(
                    false,
                    $"Minimum {_policy.MinTwitchFollowers} Twitch followers required (fleet default).");
            }
        }

        return new FleetAbuseCheckResult(true);
    }
}

public sealed class FleetAutoscaleService
{
    private readonly FleetAutoscalePolicy _policy;

    public FleetAutoscaleService(FleetAutoscalePolicy policy) => _policy = policy;

    public FleetWarmPoolState Evaluate(int activeSessions, bool anyLiveStreams, DateTimeOffset? idleSinceUtc)
    {
        var scaleZero = _policy.ScaleToZeroWhenIdle
            && !anyLiveStreams
            && activeSessions == 0
            && idleSinceUtc is { } idle
            && DateTimeOffset.UtcNow - idle >= TimeSpan.FromMinutes(_policy.IdleMinutesBeforeScaleDown);

        var targetWarm = scaleZero
            ? 0
            : Math.Min(_policy.MinWarmSnapshots, _policy.MaxConcurrentSessions);

        if (activeSessions >= _policy.MaxConcurrentSessions)
        {
            targetWarm = 0;
        }

        return new FleetWarmPoolState(
            targetWarm,
            Math.Min(targetWarm, activeSessions + (_policy.MinWarmSnapshots > 0 ? 1 : 0)),
            activeSessions,
            scaleZero,
            DateTimeOffset.UtcNow);
    }
}

public sealed class FleetIncidentRunbookService
{
    private readonly IFleetIncidentStore _incidents;

    public FleetIncidentRunbookService(IFleetIncidentStore incidents) => _incidents = incidents;

    public FleetIncident RecordForkCrash(
        string sessionId,
        string streamerId,
        bool autoRestartAttempted,
        string? detail = null)
    {
        var incident = new FleetIncident(
            Guid.NewGuid().ToString("N")[..12],
            FleetIncidentKind.ForkCrash,
            sessionId,
            streamerId,
            detail ?? "Fork process exited unexpectedly.",
            DateTimeOffset.UtcNow,
            autoRestartAttempted,
            SpectatorMessage: "The streamer's game session is restarting — one moment.");
        _incidents.Add(incident);
        return incident;
    }

    public FleetIncident RecordUnhealthyFork(string sessionId, string streamerId)
    {
        var incident = new FleetIncident(
            Guid.NewGuid().ToString("N")[..12],
            FleetIncidentKind.ForkUnhealthy,
            sessionId,
            streamerId,
            "Fork status reported disconnected beyond idle threshold.",
            DateTimeOffset.UtcNow,
            AutoRestartAttempted: true,
            SpectatorMessage: "Reconnecting game server…");
        _incidents.Add(incident);
        return incident;
    }
}

public sealed class FleetComplianceService
{
    private readonly FleetModerationRetentionPolicy _retention;

    public FleetComplianceService(FleetModerationRetentionPolicy retention) => _retention = retention;

    public FleetModerationRetentionPolicy RetentionPolicy => _retention;

    public FleetComplianceExport ExportSpProfile(string playerId, object profileDto)
    {
        if (!_retention.AllowGdprExport)
        {
            throw new InvalidOperationException("GDPR export disabled by fleet policy.");
        }

        NlFleetPaths.EnsureRoot();
        var json = System.Text.Json.JsonSerializer.Serialize(profileDto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(NlFleetPaths.ComplianceExports, $"{playerId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json");
        File.WriteAllText(path, json);
        return new FleetComplianceExport(playerId, json, DateTimeOffset.UtcNow);
    }

    public void DeleteSpProfile(string spStorePath, string playerId)
    {
        if (!_retention.AllowGdprDelete)
        {
            throw new InvalidOperationException("GDPR delete disabled by fleet policy.");
        }

        if (!File.Exists(spStorePath))
        {
            return;
        }

        var json = File.ReadAllText(spStorePath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return;
        }

        var map = new Dictionary<string, System.Text.Json.JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (!string.Equals(prop.Name, playerId, StringComparison.OrdinalIgnoreCase))
            {
                map[prop.Name] = prop.Value.Clone();
            }
        }

        var output = map.ToDictionary(k => k.Key, k => k.Value);
        File.WriteAllText(spStorePath, System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public void ApplyModerationRetention(string moderationLogPath)
    {
        if (!File.Exists(moderationLogPath) || _retention.RetentionDays <= 0)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_retention.RetentionDays);
        var kept = File.ReadAllLines(moderationLogPath)
            .Where(line =>
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return false;
                }

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("timestampUtc", out var ts)
                        && DateTimeOffset.TryParse(ts.GetString(), out var when))
                    {
                        return when >= cutoff;
                    }
                }
                catch
                {
                    // keep unparseable lines
                }

                return true;
            })
            .ToList();

        File.WriteAllLines(moderationLogPath, kept);
    }
}

public sealed class FleetSloEvaluator
{
    public IReadOnlyList<FleetSloStatus> Evaluate(
        FleetObservabilitySnapshot snapshot,
        FleetLoadTestResult? loadTest = null,
        IFleetMetricsStore? metrics = null,
        IFleetIncidentStore? incidents = null)
    {
        var admitRate = snapshot.TotalAdmits == 0
            ? 1.0
            : (double)(snapshot.TotalAdmits - snapshot.TotalAdmitDenials) / snapshot.TotalAdmits;

        if (loadTest is not null && loadTest.AdmitsSucceeded + loadTest.AdmitsFailed > 0)
        {
            admitRate = (double)loadTest.AdmitsSucceeded
                / (loadTest.AdmitsSucceeded + loadTest.AdmitsFailed);
        }

        var forkP99 = loadTest?.ForkCreateP99Ms > 0
            ? loadTest.ForkCreateP99Ms
            : metrics?.GetForkCreateP99Ms() ?? 0;

        var restartRate = ComputeAutoRestartRate(incidents);

        var list = new List<FleetSloStatus>();
        foreach (var slo in FleetSloCatalog.StagingDefaults)
        {
            var (current, met) = slo.Name switch
            {
                "concurrent_ephemeral_sessions" => ((double)snapshot.ActiveForkSessions, snapshot.ActiveForkSessions >= slo.Target),
                "admit_success_rate" => (admitRate, admitRate >= slo.Target),
                "fork_create_p99_ms" => (forkP99 <= 0 ? 1000 : forkP99, forkP99 <= 0 || forkP99 <= slo.Target),
                "incident_auto_restart_rate" => (restartRate, restartRate >= slo.Target),
                _ => (0.0, true),
            };
            list.Add(new FleetSloStatus(slo.Name, slo.Target, current, met, slo.Unit));
        }

        return list;
    }

    private static double ComputeAutoRestartRate(IFleetIncidentStore? incidents)
    {
        if (incidents is null)
        {
            return 1.0;
        }

        var recent = incidents.ListRecent(200)
            .Where(i => i.Kind is FleetIncidentKind.ForkCrash or FleetIncidentKind.ForkUnhealthy)
            .ToList();
        if (recent.Count == 0)
        {
            return 1.0;
        }

        return (double)recent.Count(i => i.AutoRestartAttempted) / recent.Count;
    }
}
