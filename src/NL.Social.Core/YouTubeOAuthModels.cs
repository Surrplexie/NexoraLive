namespace NL.Social.Core;

/// <summary>Per-player YouTube OAuth credentials (tokens stored encrypted at rest).</summary>
public sealed record YouTubeOAuthCredential(
    string PlayerId,
    string YouTubeChannelId,
    string? YouTubeChannelTitle,
    string ProtectedRefreshToken,
    string? ProtectedAccessToken = null,
    DateTimeOffset? AccessTokenExpiresUtc = null);

public sealed class YouTubeLinkConflictException : Exception
{
    public YouTubeLinkConflictException(string youTubeChannelId, string existingPlayerId)
        : base($"YouTube channel '{youTubeChannelId}' is already linked to player '{existingPlayerId}'.")
    {
        YouTubeChannelId = youTubeChannelId;
        ExistingPlayerId = existingPlayerId;
    }

    public string YouTubeChannelId { get; }

    public string ExistingPlayerId { get; }
}
