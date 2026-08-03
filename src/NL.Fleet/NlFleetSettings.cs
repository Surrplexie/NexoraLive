using NL.Fleet.Core;

namespace NL.Fleet;

public sealed class NlFleetSettings
{
    public const string EnabledVariable = "NL_FLEET_ENABLED";

    public bool Enabled { get; init; } = true;

    public string DefaultRegion { get; init; } = "us-east";

    public FleetAutoscalePolicy Autoscale { get; init; } = new(2, 128, true, 15);

    public FleetAbusePolicy Abuse { get; init; } = new(6, 50, 30);

    public FleetModerationRetentionPolicy Retention { get; init; } = new(730, true, true);

    public FleetRelayConfig Relay { get; init; } = new(
        "wss://relay-{region}.nl.example.com/fork/{session}",
        "turn:turn.nl.example.com:3478");

    public static NlFleetSettings LoadFromEnvironment()
    {
        var enabledRaw = Environment.GetEnvironmentVariable(EnabledVariable);
        var enabled = enabledRaw is null
            || string.Equals(enabledRaw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enabledRaw, "true", StringComparison.OrdinalIgnoreCase);

        var maxSessions = int.TryParse(Environment.GetEnvironmentVariable("NL_FLEET_MAX_CONCURRENT"), out var max)
            ? Math.Max(1, max)
            : 128;

        var minWarm = int.TryParse(Environment.GetEnvironmentVariable("NL_FLEET_MIN_WARM"), out var warm)
            ? Math.Max(0, warm)
            : 2;

        var minFollowers = int.TryParse(Environment.GetEnvironmentVariable("NL_FLEET_MIN_TWITCH_FOLLOWERS"), out var fol)
            ? Math.Max(0, fol)
            : 50;

        var forkPerMin = int.TryParse(Environment.GetEnvironmentVariable("NL_FLEET_FORK_CREATE_RATE_PER_MIN"), out var rate)
            ? Math.Max(1, rate)
            : 30;

        var forkPerHour = int.TryParse(Environment.GetEnvironmentVariable("NL_FLEET_MAX_FORK_CREATES_PER_HOUR"), out var perHour)
            ? Math.Max(1, perHour)
            : 6;

        var retentionDays = int.TryParse(Environment.GetEnvironmentVariable("NL_FLEET_MOD_RETENTION_DAYS"), out var days)
            ? Math.Max(30, days)
            : 730;

        return new NlFleetSettings
        {
            Enabled = enabled,
            DefaultRegion = Environment.GetEnvironmentVariable("NL_FLEET_DEFAULT_REGION") ?? "us-east",
            Autoscale = new FleetAutoscalePolicy(minWarm, maxSessions, true, 15),
            Abuse = new FleetAbusePolicy(forkPerHour, minFollowers, forkPerMin),
            Retention = new FleetModerationRetentionPolicy(retentionDays, true, true),
            Relay = new FleetRelayConfig(
                Environment.GetEnvironmentVariable("NL_FLEET_RELAY_WS_TEMPLATE")
                ?? "wss://relay-{region}.nl.example.com/fork/{session}",
                Environment.GetEnvironmentVariable("NL_FLEET_TURN_URI") ?? "turn:turn.nl.example.com:3478"),
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        defaultRegion = DefaultRegion,
        autoscale = Autoscale,
        abuse = Abuse,
        retention = Retention,
        relay = new { Relay.RelayWebSocketTemplate, Relay.TurnUri, Relay.MaskRawHostIps },
        storePath = NlFleetPaths.Root,
        slos = FleetSloCatalog.StagingDefaults,
    };
}

public sealed class NlFleetHost
{
    public NlFleetHost(NlFleetSettings settings)
    {
        Settings = settings;
        NlFleetPaths.EnsureRoot();

        Metrics = new JsonFleetMetricsStore();
        Incidents = new JsonFleetIncidentStore();
        StreamerRequirements = new JsonFleetStreamerRequirementsStore();
        Regions = new FleetRegionService();
        Relay = new FleetRelayService(settings.Relay);
        Abuse = new FleetAbuseGateService(settings.Abuse, Metrics, StreamerRequirements);
        Autoscale = new FleetAutoscaleService(settings.Autoscale);
        Runbook = new FleetIncidentRunbookService(Incidents);
        Compliance = new FleetComplianceService(settings.Retention);
        Slo = new FleetSloEvaluator();
        Validation = new FleetStagingValidationService();
        ValidationStore = new JsonFleetValidationStore();
    }

    public NlFleetSettings Settings { get; }

    public JsonFleetMetricsStore Metrics { get; }

    public JsonFleetIncidentStore Incidents { get; }

    public JsonFleetStreamerRequirementsStore StreamerRequirements { get; }

    public FleetRegionService Regions { get; }

    public FleetRelayService Relay { get; }

    public FleetAbuseGateService Abuse { get; }

    public FleetAutoscaleService Autoscale { get; }

    public FleetIncidentRunbookService Runbook { get; }

    public FleetComplianceService Compliance { get; }

    public FleetSloEvaluator Slo { get; }

    public FleetStagingValidationService Validation { get; }

    public JsonFleetValidationStore ValidationStore { get; }
}
