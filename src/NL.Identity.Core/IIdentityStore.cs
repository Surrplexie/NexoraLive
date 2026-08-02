namespace NL.Identity.Core;

public sealed class PlatformLinkConflictException : Exception
{
    public PlatformLinkConflictException(string platformKey, string existingAccountId)
        : base($"Platform identity '{platformKey}' is already linked to NL account '{existingAccountId}'.")
    {
        PlatformKey = platformKey;
        ExistingAccountId = existingAccountId;
    }

    public string PlatformKey { get; }

    public string ExistingAccountId { get; }
}

public interface IIdentityStore
{
    NlIdentityAccount? GetAccount(string accountId);

    NlIdentityAccount? GetAccountByPlatformLink(NlPlatform platform, string externalUserId);

    string? GetAccountIdForPlatformLink(NlPlatform platform, string externalUserId);

    void SaveAccount(NlIdentityAccount account);

    void DeleteAccount(string accountId);

    IReadOnlyList<NlIdentityAccount> ListAccounts();
}

public interface IIdentityAuditStore
{
    void Append(NlIdentityAuditEvent entry);

    IReadOnlyList<NlIdentityAuditEvent> ReadRecent(int count = 100);
}
