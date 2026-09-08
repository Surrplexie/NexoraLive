namespace NL.Social.Core;

/// <summary>Per-player Kick OAuth credentials (tokens stored encrypted at rest).</summary>
public sealed record KickOAuthCredential(
    string PlayerId,
    string KickUserId,
    string? KickUsername,
    string ProtectedRefreshToken,
    string? ProtectedAccessToken = null,
    DateTimeOffset? AccessTokenExpiresUtc = null);

public sealed class KickLinkConflictException : Exception
{
    public KickLinkConflictException(string kickUserId, string existingPlayerId)
        : base($"Kick user '{kickUserId}' is already linked to player '{existingPlayerId}'.")
    {
        KickUserId = kickUserId;
        ExistingPlayerId = existingPlayerId;
    }

    public string KickUserId { get; }

    public string ExistingPlayerId { get; }
}
