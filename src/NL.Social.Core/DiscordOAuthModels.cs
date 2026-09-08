namespace NL.Social.Core;

/// <summary>Per-player Discord OAuth credentials (tokens stored encrypted at rest).</summary>
public sealed record DiscordOAuthCredential(
    string PlayerId,
    string DiscordUserId,
    string? DiscordUsername,
    string ProtectedRefreshToken,
    string? ProtectedAccessToken = null,
    DateTimeOffset? AccessTokenExpiresUtc = null);

public sealed class DiscordLinkConflictException : Exception
{
    public DiscordLinkConflictException(string discordUserId, string existingPlayerId)
        : base($"Discord user '{discordUserId}' is already linked to player '{existingPlayerId}'.")
    {
        DiscordUserId = discordUserId;
        ExistingPlayerId = existingPlayerId;
    }

    public string DiscordUserId { get; }

    public string ExistingPlayerId { get; }
}

public interface IDiscordGuildMembershipChecker
{
    Task<bool?> TryGetMembershipAsync(
        SocialGateContext context,
        CancellationToken cancellationToken = default);
}
