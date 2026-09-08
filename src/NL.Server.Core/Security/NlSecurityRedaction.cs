namespace NL.Server.Core.Security;

/// <summary>Redacts bridge secrets from public API responses unless the caller is an operator.</summary>
public static class NlSecurityRedaction
{
    public const string RedactedTokenPlaceholder = "REDACTED";

    public static Integration.NlSessionManifest RedactManifest(Integration.NlSessionManifest manifest, bool includeSecrets)
    {
        if (includeSecrets)
        {
            return manifest;
        }

        return new Integration.NlSessionManifest
        {
            SessionId = manifest.SessionId,
            StreamerId = manifest.StreamerId,
            HttpBaseUrl = manifest.HttpBaseUrl,
            BridgeConnectUrl = RedactBridgeUrl(manifest.BridgeConnectUrl),
            AdmitUrl = manifest.AdmitUrl,
            ManifestUrl = manifest.ManifestUrl,
            ModerationUrl = manifest.ModerationUrl,
            JoinGateEnabled = manifest.JoinGateEnabled,
            SessionRunning = manifest.SessionRunning,
            AntiCheatEnabled = manifest.AntiCheatEnabled,
            OwnershipRequired = manifest.OwnershipRequired,
            GameId = manifest.GameId,
            PlatformAppId = manifest.PlatformAppId,
            CatalogMajorVersion = manifest.CatalogMajorVersion,
            PartnershipTier = manifest.PartnershipTier,
            NoProgressTransfer = manifest.NoProgressTransfer,
            CatalogLegalNotice = manifest.CatalogLegalNotice,
            RequiresAtOwnRiskAcknowledgment = manifest.RequiresAtOwnRiskAcknowledgment,
            PartnershipLegalUrl = manifest.PartnershipLegalUrl,
            PartnershipDisclaimerVersion = manifest.PartnershipDisclaimerVersion,
            ForkConnectEndpoint = manifest.ForkConnectEndpoint,
            ForkSessionId = manifest.ForkSessionId,
            ForkProvisioner = manifest.ForkProvisioner,
            ForkOrchestratorEnabled = manifest.ForkOrchestratorEnabled,
            ReservedPrivilegedSlots = manifest.ReservedPrivilegedSlots,
            FleetRegionId = manifest.FleetRegionId,
            FleetTurnUri = manifest.FleetTurnUri,
        };
    }

    public static Integration.NlSessionBusInfo RedactBusInfo(Integration.NlSessionBusInfo bus, bool includeSecrets)
    {
        if (includeSecrets)
        {
            return bus;
        }

        return new Integration.NlSessionBusInfo
        {
            SessionId = bus.SessionId,
            Token = RedactedTokenPlaceholder,
            WebSocketUrl = bus.WebSocketUrl,
            HttpBaseUrl = bus.HttpBaseUrl,
            WebSocketPort = bus.WebSocketPort,
            HttpPort = bus.HttpPort,
        };
    }

    public static object RedactStatusBus(Integration.NlSessionBusInfo bus, bool includeSecrets) =>
        includeSecrets
            ? bus
            : new
            {
                bus.SessionId,
                Token = RedactedTokenPlaceholder,
                bus.WebSocketUrl,
                bus.HttpBaseUrl,
                bus.WebSocketPort,
                bus.HttpPort,
                bridgeConnectUrl = RedactBridgeUrl(bus.BridgeConnectUrl),
            };

    public static string RedactBridgeUrl(string bridgeConnectUrl)
    {
        var idx = bridgeConnectUrl.IndexOf("?token=", StringComparison.OrdinalIgnoreCase);
        return idx >= 0
            ? bridgeConnectUrl[..idx] + "?token=" + Uri.EscapeDataString(RedactedTokenPlaceholder)
            : bridgeConnectUrl;
    }
}
