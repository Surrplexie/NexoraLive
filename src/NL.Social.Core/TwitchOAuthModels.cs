namespace NL.Social.Core;

/// <summary>Per-player Twitch OAuth credentials (tokens stored encrypted at rest).</summary>
public sealed record TwitchOAuthCredential(
    string PlayerId,
    string TwitchUserId,
    string? TwitchLogin,
    string ProtectedRefreshToken,
    string? ProtectedAccessToken = null,
    DateTimeOffset? AccessTokenExpiresUtc = null);

public sealed class TwitchLinkConflictException : Exception
{
    public TwitchLinkConflictException(string twitchUserId, string existingPlayerId)
        : base($"Twitch user '{twitchUserId}' is already linked to player '{existingPlayerId}'.")
    {
        TwitchUserId = twitchUserId;
        ExistingPlayerId = existingPlayerId;
    }

    public string TwitchUserId { get; }

    public string ExistingPlayerId { get; }
}
