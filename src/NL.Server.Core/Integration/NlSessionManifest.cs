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

    public static NlJoinAdmissionResult FromJoinResult(JoinResult join, string playerId, SpStanding standing) =>
        new()
        {
            Decision = join.Decision,
            Reason = join.Reason,
            PlayerId = playerId,
            Admit = join.Decision == JoinDecision.Allow,
            Standing = standing,
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
}

public static class NlSessionServerDefaults
{
    public const int HttpPort = NlSessionBusDefaults.HttpPort;
    public const int WebSocketPort = NlSessionBusDefaults.WebSocketPort;
    public const int ModerationPort = 27030;
}
