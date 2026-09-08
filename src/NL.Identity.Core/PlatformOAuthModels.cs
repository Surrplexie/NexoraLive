namespace NL.Identity.Core;

/// <summary>Per-account platform OAuth credentials (tokens encrypted at rest).</summary>
public sealed record PlatformOAuthCredential(
    string AccountId,
    NlPlatform Platform,
    string ExternalUserId,
    string? DisplayName,
    string ProtectedRefreshToken,
    string? ProtectedAccessToken = null,
    DateTimeOffset? AccessTokenExpiresUtc = null,
    string? MetadataJson = null);

public sealed record PlatformOAuthCallbackResult(
    bool Success,
    NlPlatform Platform,
    string? AccountId = null,
    string? ExternalUserId = null,
    string? DisplayName = null,
    string? ReturnUrl = null,
    string? Error = null);
