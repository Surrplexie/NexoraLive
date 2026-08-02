namespace NL.Partnership;

public sealed class NlPartnershipSettings
{
    public const string EnabledVariable = "NL_PARTNERSHIP_ENABLED";

    public bool Enabled { get; init; } = true;

    public bool RequireGateAtAdmit { get; init; } = true;

    public string? WebhookSecret { get; init; }

    public static NlPartnershipSettings LoadFromEnvironment()
    {
        var enabledRaw = Environment.GetEnvironmentVariable(EnabledVariable);
        var enabled = enabledRaw is null
            || string.Equals(enabledRaw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(enabledRaw, "true", StringComparison.OrdinalIgnoreCase);

        var requireGate = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PARTNERSHIP_GATE_ADMIT"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        return new NlPartnershipSettings
        {
            Enabled = enabled,
            RequireGateAtAdmit = requireGate,
            WebhookSecret = Environment.GetEnvironmentVariable("NL_PARTNERSHIP_WEBHOOK_SECRET"),
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        requireGateAtAdmit = RequireGateAtAdmit,
        webhookConfigured = !string.IsNullOrWhiteSpace(WebhookSecret),
        storePath = NlPartnershipPaths.Root,
        disclaimerVersion = NL.Partnership.Core.PartnershipLegalTemplates.DisclaimerVersion,
    };
}

public sealed class NlPartnershipHost
{
    public NlPartnershipHost(NlPartnershipSettings settings)
    {
        Settings = settings;
        NlPartnershipPaths.EnsureRoot();

        Acknowledgments = new JsonAtOwnRiskAcknowledgmentStore();
        Publishers = new JsonPublisherRegistry();
        PlatformOptIn = new JsonPlatformOptInStore();
        Bans = new JsonPublisherBanStore();
        Metrics = new JsonPublisherSessionMetricsStore();
        Audit = new JsonlPartnershipAuditStore();

        Gate = new PartnershipGateService(Acknowledgments, Bans, PlatformOptIn, Publishers);
        BanSync = new BanSyncWebhookService(Bans, Audit);
        Dashboard = new PublisherDashboardService(Publishers, Bans, Metrics);
    }

    public NlPartnershipSettings Settings { get; }

    public JsonAtOwnRiskAcknowledgmentStore Acknowledgments { get; }

    public JsonPublisherRegistry Publishers { get; }

    public JsonPlatformOptInStore PlatformOptIn { get; }

    public JsonPublisherBanStore Bans { get; }

    public JsonPublisherSessionMetricsStore Metrics { get; }

    public JsonlPartnershipAuditStore Audit { get; }

    public PartnershipGateService Gate { get; }

    public BanSyncWebhookService BanSync { get; }

    public PublisherDashboardService Dashboard { get; }
}
