namespace NL.Client.Core;

public enum NlClientMode
{
    Player,
    Streamer,
    MobileCompanion,
}

public enum NlClientJoinStep
{
    Completed,
    RequiresOwnership,
    RequiresAtOwnRiskAck,
    AdmitDenied,
    SessionOffline,
    Error,
}

public sealed record NlClientDeepLinkRequest(
    string StreamerId,
    string GameId,
    string MajorVersion,
    string? PlayerId = null);

public sealed record NlClientJoinRequest(
    string PlayerId,
    string StreamerId,
    string? DisplayName = null,
    string? GameId = null,
    string? MajorVersion = null,
    string? PlatformUserId = null,
    string? Platform = null,
    string? AppId = null,
    bool AtOwnRiskAcknowledged = false,
    NlClientMode Mode = NlClientMode.Player);

public sealed record NlClientAdmitResponse(
    bool Admit,
    string? Reason,
    string? Decision,
    bool RequiresAtOwnRiskAcknowledgment,
    string? PartnershipTier,
    string? PartnershipLegalUrl);

public sealed record NlClientManifest(
    string SessionId,
    string StreamerId,
    string HttpBaseUrl,
    string BridgeConnectUrl,
    string AdmitUrl,
    string? ForkConnectEndpoint,
    string? PartnershipTier,
    bool RequiresAtOwnRiskAcknowledgment,
    bool SessionRunning,
    string? GameId,
    string? CatalogMajorVersion);

public sealed record NlClientLaunchParams(
    string Scheme,
    string CommandLine,
    string ForkConnectEndpoint,
    string BridgeConnectUrl,
    string? DeepLink);

public sealed record NlClientJoinFlowResult(
    bool Success,
    NlClientJoinStep Step,
    string? Message = null,
    NlClientManifest? Manifest = null,
    NlClientLaunchParams? Launch = null,
    NlClientAdmitResponse? Admit = null);

public sealed record NlClientStreamerInfo(
    string StreamerId,
    bool IsLive,
    string? Title,
    string? Platform,
    string? GameId);

public sealed record NlClientOverlayState(
    string PlayerId,
    string StreamerId,
    string Standing,
    int ActiveOffenseCount,
    IReadOnlyList<string> RecentWarnings,
    bool ClipTriggerAvailable,
    DateTimeOffset UpdatedAtUtc);

public sealed record NlClientInviteBlockResult(
    bool Blocked,
    string? Reason,
    string? RedirectUrl = null);

public sealed record NlClientMobileActionRequest(
    string PlayerId,
    string StreamerId,
    string Action,
    string? Reason = null);

public sealed record NlClientMobileActionResult(
    bool Success,
    string? Error = null);
