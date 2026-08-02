using NL.Core.Sp;
using NL.Identity.Core;

namespace NL.Identity;

public sealed class NlOwnershipAdmissionGate
{
    private readonly IGameOwnershipVerifier _ownership;
    private readonly IPublisherBanChecker _banChecker;
    private readonly IMultiplayerSubscriptionChecker _subscriptionChecker;
    private readonly NlIdentityService _identity;
    private readonly NlIdentitySettings _settings;
    private readonly IIdentityAuditStore _audit;

    public NlOwnershipAdmissionGate(
        IGameOwnershipVerifier ownership,
        IPublisherBanChecker banChecker,
        IMultiplayerSubscriptionChecker subscriptionChecker,
        NlIdentityService identity,
        NlIdentitySettings settings,
        IIdentityAuditStore? audit = null)
    {
        _ownership = ownership;
        _banChecker = banChecker;
        _subscriptionChecker = subscriptionChecker;
        _identity = identity;
        _settings = settings;
        _audit = audit ?? new JsonlIdentityAuditStore();
    }

    public async Task<OwnershipAdmissionResult?> EvaluateAsync(
        OwnershipAdmissionContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.RequireGameOwnership)
        {
            return null;
        }

        if (!_settings.Enabled)
        {
            return null;
        }

        if (context.Mode == NlOwnershipMode.Off)
        {
            return Deny("Game ownership required but NL_OWNERSHIP_MODE=off.");
        }

        if (!NlPlatformNames.TryParse(context.Platform, out var platform))
        {
            return Deny("Platform required for ownership verification (e.g. steam).");
        }

        if (string.IsNullOrWhiteSpace(context.PlatformUserId))
        {
            return Deny("platformUserId required for ownership verification.");
        }

        var platformUserId = context.PlatformUserId.Trim();

        if (_settings.EnforceOneLinkPerPlatform)
        {
            var linkedAccount = _identity.GetAccountByPlatform(platform, platformUserId);
            if (linkedAccount is not null
                && !string.IsNullOrWhiteSpace(context.NlAccountId)
                && !string.Equals(linkedAccount.Id, context.NlAccountId, StringComparison.Ordinal))
            {
                _audit.Append(new NlIdentityAuditEvent(
                    NlIdentityAuditKind.OwnershipDenied,
                    context.NlAccountId,
                    NlPlatformNames.LinkKey(platform, platformUserId),
                    $"Anti-alt: platform linked to {linkedAccount.Id}",
                    DateTimeOffset.UtcNow));
                return Deny("This platform account is already linked to a different NL account.");
            }
        }

        var gameId = context.GameId ?? "unknown";
        var appId = context.AppId ?? context.GameId;

        if (await _banChecker.IsPublisherBannedAsync(platform, platformUserId, gameId, appId, cancellationToken))
        {
            _audit.Append(new NlIdentityAuditEvent(
                NlIdentityAuditKind.OwnershipDenied,
                context.NlAccountId,
                NlPlatformNames.LinkKey(platform, platformUserId),
                "Publisher ban",
                DateTimeOffset.UtcNow));
            return Deny("Publisher ban active for this title — NL cannot bypass.");
        }

        if (!await _subscriptionChecker.HasMultiplayerAccessAsync(platform, platformUserId, gameId, cancellationToken))
        {
            return Deny("Multiplayer subscription required on this platform.");
        }

        var ownership = await _ownership.VerifyAsync(
            new GameOwnershipRequest(platform, platformUserId, gameId, appId, context.MajorVersion),
            cancellationToken);

        switch (ownership.Status)
        {
            case GameOwnershipStatus.Owned:
                _audit.Append(new NlIdentityAuditEvent(
                    NlIdentityAuditKind.OwnershipVerified,
                    context.NlAccountId,
                    NlPlatformNames.LinkKey(platform, platformUserId),
                    $"Owned {appId}",
                    DateTimeOffset.UtcNow));
                return null;
            case GameOwnershipStatus.NotOwned:
            case GameOwnershipStatus.Banned:
            case GameOwnershipStatus.SubscriptionRequired:
                _audit.Append(new NlIdentityAuditEvent(
                    NlIdentityAuditKind.OwnershipDenied,
                    context.NlAccountId,
                    NlPlatformNames.LinkKey(platform, platformUserId),
                    ownership.Message ?? ownership.Status.ToString(),
                    DateTimeOffset.UtcNow));
                return Deny(ownership.Message ?? ownership.Status.ToString());
            case GameOwnershipStatus.Unknown:
                if (context.StrictUnknown)
                {
                    return Deny(ownership.Message ?? "Ownership could not be verified.");
                }

                return null;
            default:
                return Deny("Ownership verification failed.");
        }
    }

    private static OwnershipAdmissionResult Deny(string reason) =>
        new(JoinDecision.Deny, reason, GameOwnershipStatus.NotOwned);
}

public sealed record OwnershipAdmissionContext(
    bool RequireGameOwnership,
    NlOwnershipMode Mode,
    string? Platform,
    string? PlatformUserId,
    string? GameId,
    string? AppId,
    string? MajorVersion,
    string? NlAccountId,
    bool StrictUnknown);

public sealed record OwnershipAdmissionResult(
    JoinDecision Decision,
    string Reason,
    GameOwnershipStatus OwnershipStatus);
