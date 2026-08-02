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

    public List<NlPlatformLink> Links { get; init; } = [];
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
}

public sealed record NlIdentityAuditEvent(
    NlIdentityAuditKind Kind,
    string? AccountId,
    string? PlatformKey,
    string Message,
    DateTimeOffset TimestampUtc);
