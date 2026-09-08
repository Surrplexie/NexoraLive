namespace NL.Identity.Core;

public enum GameOwnershipStatus
{
    Owned,
    NotOwned,
    Unknown,
    Banned,
    SubscriptionRequired,
}

public sealed record GameOwnershipRequest(
    NlPlatform Platform,
    string PlatformUserId,
    string GameId,
    string? AppId = null,
    string? MajorVersion = null);

public sealed record GameOwnershipResult(
    GameOwnershipStatus Status,
    string? Message = null,
    bool PublisherBanned = false,
    bool MultiplayerSubscriptionActive = true);

public interface IGameOwnershipVerifier
{
    Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default);
}

public interface IPublisherBanChecker
{
    Task<bool> IsPublisherBannedAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        string? appId = null,
        CancellationToken cancellationToken = default);
}

public interface IMultiplayerSubscriptionChecker
{
    Task<bool> HasMultiplayerAccessAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        CancellationToken cancellationToken = default);
}
