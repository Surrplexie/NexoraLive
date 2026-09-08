namespace NL.Identity.Core;

public sealed class NlPlatformLink
{
    public required NlPlatform Platform { get; init; }

    public required string ExternalUserId { get; init; }

    public DateTimeOffset LinkedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>DPAPI/AES-protected refresh token blob (optional).</summary>
    public string? ProtectedRefreshToken { get; set; }

    public DateTimeOffset? TokenExpiresAtUtc { get; set; }
}

public sealed class NlIdentityAccount
{
    public required string Id { get; init; }

    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Pending or verified email address.</summary>
    public string? Email { get; set; }

    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }

    /// <summary>DPAPI/AES-protected base32 TOTP secret when 2FA enrolled.</summary>
    public string? ProtectedTotpSecret { get; set; }

    public DateTimeOffset? TwoFactorEnabledAtUtc { get; set; }

    /// <summary>When set, this account may operate as a streamer under this id.</summary>
    public string? StreamerId { get; set; }

    public DateTimeOffset? StreamerEnabledAtUtc { get; set; }

    /// <summary>PBKDF2 password hash (format pbkdf2:iter:salt:hash).</summary>
    public string? ProtectedPasswordHash { get; set; }

    public List<NlPlatformLink> Links { get; init; } = [];

    public string PlayerId => Id;

    public NlAccountCapabilities Capabilities
    {
        get
        {
            var caps = NlAccountCapabilities.Player;
            if (!string.IsNullOrWhiteSpace(StreamerId))
            {
                caps |= NlAccountCapabilities.Streamer;
            }

            return caps;
        }
    }
}

public sealed class NlGameCatalogEntry
{
    public required string GameId { get; init; }

    public required NlPlatform Platform { get; init; }

    /// <summary>Platform store app id (e.g. Steam app id).</summary>
    public required string AppId { get; init; }

    public string? MajorVersion { get; init; }

    public string? DisplayName { get; init; }
}

public enum NlIdentityAuditKind
{
    AccountCreated,
    PlatformLinked,
    PlatformLinkRejected,
    PlatformUnlinked,
    OwnershipVerified,
    OwnershipDenied,
    TokenRotated,
    EmailVerificationSent,
    EmailVerified,
    TwoFactorEnrollStarted,
    TwoFactorEnabled,
    TwoFactorDisabled,
    TwoFactorChallengeFailed,
    AccountRegistered,
    AccountLoggedIn,
    AccountLoggedOut,
    StreamerEnabled,
    StreamerDisabled,
}

public sealed record NlIdentityAuditEvent(
    NlIdentityAuditKind Kind,
    string? AccountId,
    string? PlatformKey,
    string Message,
    DateTimeOffset TimestampUtc);
