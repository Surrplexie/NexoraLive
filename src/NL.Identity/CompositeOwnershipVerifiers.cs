using NL.Identity.Core;

namespace NL.Identity;

/// <summary>Routes ownership checks to the first verifier that handles the platform.</summary>
public sealed class CompositeGameOwnershipVerifier : IGameOwnershipVerifier
{
    private readonly IReadOnlyList<IGameOwnershipVerifier> _verifiers;

    public CompositeGameOwnershipVerifier(params IGameOwnershipVerifier[] verifiers) =>
        _verifiers = verifiers.ToList();

    public async Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        GameOwnershipResult? last = null;
        foreach (var verifier in _verifiers)
        {
            var result = await verifier.VerifyAsync(request, cancellationToken);
            last = result;
            if (result.Status is not GameOwnershipStatus.Unknown)
            {
                return result;
            }
        }

        return last ?? new GameOwnershipResult(GameOwnershipStatus.Unknown, "No ownership verifier configured.");
    }
}

public sealed class CompositePublisherBanChecker : IPublisherBanChecker
{
    private readonly IReadOnlyList<IPublisherBanChecker> _checkers;

    public CompositePublisherBanChecker(params IPublisherBanChecker[] checkers) =>
        _checkers = checkers.ToList();

    public async Task<bool> IsPublisherBannedAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        string? appId = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var checker in _checkers)
        {
            if (await checker.IsPublisherBannedAsync(platform, platformUserId, gameId, appId, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class CompositeMultiplayerSubscriptionChecker : IMultiplayerSubscriptionChecker
{
    private readonly IReadOnlyList<IMultiplayerSubscriptionChecker> _checkers;

    public CompositeMultiplayerSubscriptionChecker(params IMultiplayerSubscriptionChecker[] checkers) =>
        _checkers = checkers.ToList();

    public async Task<bool> HasMultiplayerAccessAsync(
        NlPlatform platform,
        string platformUserId,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        foreach (var checker in _checkers)
        {
            if (!await checker.HasMultiplayerAccessAsync(platform, platformUserId, gameId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Placeholder for non-Steam platforms until live OAuth is wired (Phase L stub).</summary>
public sealed class StubPlatformOwnershipVerifier : IGameOwnershipVerifier
{
    private readonly NlPlatform _platform;

    public StubPlatformOwnershipVerifier(NlPlatform platform) => _platform = platform;

    public Task<GameOwnershipResult> VerifyAsync(GameOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != _platform)
        {
            return Task.FromResult(new GameOwnershipResult(GameOwnershipStatus.Unknown));
        }

        return Task.FromResult(new GameOwnershipResult(
            GameOwnershipStatus.Unknown,
            $"{_platform} live ownership API not configured; use mock-ownership.json or Steam for dev."));
    }
}
