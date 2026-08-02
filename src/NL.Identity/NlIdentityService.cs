using NL.Identity.Core;

namespace NL.Identity;

public sealed class NlIdentityService
{
    private readonly IIdentityStore _store;
    private readonly IIdentityAuditStore _audit;
    private readonly NlTokenProtector _tokenProtector;

    public NlIdentityService(
        IIdentityStore store,
        IIdentityAuditStore audit,
        NlTokenProtector? tokenProtector = null)
    {
        _store = store;
        _audit = audit;
        _tokenProtector = tokenProtector ?? new NlTokenProtector();
    }

    public NlIdentityAccount CreateAccount(string displayName)
    {
        var account = new NlIdentityAccount
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = displayName.Trim(),
        };
        _store.SaveAccount(account);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.AccountCreated,
            account.Id,
            null,
            $"Account created for {account.DisplayName}",
            DateTimeOffset.UtcNow));
        return account;
    }

    public NlIdentityAccount LinkPlatform(
        string accountId,
        NlPlatform platform,
        string externalUserId,
        string? refreshToken = null,
        DateTimeOffset? tokenExpiresAtUtc = null)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");

        externalUserId = externalUserId.Trim();
        var platformKey = NlPlatformNames.LinkKey(platform, externalUserId);
        var existingAccountId = _store.GetAccountIdForPlatformLink(platform, externalUserId);
        if (existingAccountId is not null && !string.Equals(existingAccountId, accountId, StringComparison.Ordinal))
        {
            _audit.Append(new NlIdentityAuditEvent(
                NlIdentityAuditKind.PlatformLinkRejected,
                accountId,
                platformKey,
                $"Rejected: already linked to {existingAccountId}",
                DateTimeOffset.UtcNow));
            throw new PlatformLinkConflictException(platformKey, existingAccountId);
        }

        account.Links.RemoveAll(l =>
            l.Platform == platform && string.Equals(l.ExternalUserId, externalUserId, StringComparison.Ordinal));

        var link = new NlPlatformLink
        {
            Platform = platform,
            ExternalUserId = externalUserId,
            LinkedAtUtc = DateTimeOffset.UtcNow,
            TokenExpiresAtUtc = tokenExpiresAtUtc,
        };

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            link.ProtectedRefreshToken = _tokenProtector.Protect(refreshToken);
        }

        account.Links.Add(link);
        _store.SaveAccount(account);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.PlatformLinked,
            accountId,
            platformKey,
            "Platform linked",
            DateTimeOffset.UtcNow));
        return account;
    }

    public NlIdentityAccount? GetAccount(string accountId) => _store.GetAccount(accountId);

    public NlIdentityAccount? GetAccountByPlatform(NlPlatform platform, string externalUserId) =>
        _store.GetAccountByPlatformLink(platform, externalUserId);

    public void UnlinkPlatform(string accountId, NlPlatform platform, string externalUserId)
    {
        var account = _store.GetAccount(accountId)
            ?? throw new InvalidOperationException($"NL account '{accountId}' not found.");
        var removed = account.Links.RemoveAll(l =>
            l.Platform == platform && string.Equals(l.ExternalUserId, externalUserId.Trim(), StringComparison.Ordinal));
        if (removed == 0)
        {
            return;
        }

        _store.SaveAccount(account);
        _audit.Append(new NlIdentityAuditEvent(
            NlIdentityAuditKind.PlatformUnlinked,
            accountId,
            NlPlatformNames.LinkKey(platform, externalUserId),
            "Platform unlinked",
            DateTimeOffset.UtcNow));
    }
}
