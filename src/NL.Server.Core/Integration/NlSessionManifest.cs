using NL.Core.Sp;

namespace NL.Server.Core.Integration;

/// <summary>Pre-connect join admission result (Phase D networked session server).</summary>
public sealed class NlJoinAdmissionResult
{
    public required JoinDecision Decision { get; init; }
    public required string? Reason { get; init; }
    public required string PlayerId { get; init; }
    public required bool Admit { get; init; }
    public SpStanding Standing { get; init; }
    public string? OwnershipStatus { get; init; }
    public string? PartnershipTier { get; init; }

    /// <summary>Phase Q — SP must acknowledge at-own-risk disclaimer before admit.</summary>
    public bool RequiresAtOwnRiskAcknowledgment { get; init; }

    public string? PartnershipLegalUrl { get; init; }

    public string? PartnershipDisclaimerVersion { get; init; }

    public static NlJoinAdmissionResult FromJoinResult(JoinResult join, string playerId, SpStanding standing, string? partnershipTier = null) =>
        new()
        {
            Decision = join.Decision,
            Reason = join.Reason,
            PlayerId = playerId,
            Admit = join.Decision == JoinDecision.Allow,
            Standing = standing,
            PartnershipTier = partnershipTier,
        };

    public static NlJoinAdmissionResult FromOwnershipDeny(string playerId, string reason, SpStanding standing, string ownershipStatus) =>
        new()
        {
            Decision = JoinDecision.Deny,
            Reason = reason,
            PlayerId = playerId,
            Admit = false,
            Standing = standing,
            OwnershipStatus = ownershipStatus,
        };

    public static NlJoinAdmissionResult FromCatalogDeny(
        string playerId,
        string reason,
        SpStanding standing,
        string? partnershipTier = null) =>
        new()
        {
            Decision = JoinDecision.Deny,
            Reason = reason,
            PlayerId = playerId,
            Admit = false,
            Standing = standing,
            PartnershipTier = partnershipTier,
        };

    public static NlJoinAdmissionResult FromPartnershipDeny(
        string playerId,
        string reason,
        SpStanding standing,
        string? partnershipTier,
        bool requiresAcknowledgment,
        string? legalUrl = null,
        string? disclaimerVersion = null) =>
        new()
        {
            Decision = requiresAcknowledgment ? JoinDecision.Hold : JoinDecision.Deny,
            Reason = reason,
            PlayerId = playerId,
            Admit = false,
            Standing = standing,
            PartnershipTier = partnershipTier,
            RequiresAtOwnRiskAcknowledgment = requiresAcknowledgment,
            PartnershipLegalUrl = legalUrl,
            PartnershipDisclaimerVersion = disclaimerVersion,
        };
}

/// <summary>Connection manifest for remote game bridges (Phase D).</summary>
public sealed class NlSessionManifest
{
    public required string SessionId { get; init; }
    public required string StreamerId { get; init; }
    public required string HttpBaseUrl { get; init; }
    public required string BridgeConnectUrl { get; init; }
    public required string AdmitUrl { get; init; }
    public required string ManifestUrl { get; init; }
    public required string ModerationUrl { get; init; }
    public bool JoinGateEnabled { get; init; }
    public bool SessionRunning { get; init; }
    public bool AntiCheatEnabled { get; init; }
    public bool OwnershipRequired { get; init; }
    public string? GameId { get; init; }
    public string? PlatformAppId { get; init; }

    /// <summary>Phase N — catalog major + partnership metadata for join UX.</summary>
    public string? CatalogMajorVersion { get; init; }

    public string? PartnershipTier { get; init; }

    public bool NoProgressTransfer { get; init; }

    public string? CatalogLegalNotice { get; init; }

    /// <summary>Phase Q — at-own-risk titles require one-time SP acknowledgment.</summary>
    public bool RequiresAtOwnRiskAcknowledgment { get; init; }

    public string? PartnershipLegalUrl { get; init; }

    public string? PartnershipDisclaimerVersion { get; init; }
    public string? ForkConnectEndpoint { get; init; }

    public string? ForkSessionId { get; init; }

    public string? ForkProvisioner { get; init; }

    public bool ForkOrchestratorEnabled { get; init; }

    public int ReservedPrivilegedSlots { get; init; }
}

/// <summary>Phase O — optional fork instance metadata for session manifest.</summary>
public sealed record ForkManifestConnectInfo(
    string? ForkSessionId,
    string? ForkConnectEndpoint,
    string? ForkProvisioner,
    int ReservedPrivilegedSlots = 2);

public static class NlSessionServerDefaults
{
    public const int HttpPort = NlSessionBusDefaults.HttpPort;
    public const int WebSocketPort = NlSessionBusDefaults.WebSocketPort;
    public const int ModerationPort = 27030;
}
