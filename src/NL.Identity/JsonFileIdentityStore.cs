using System.Text.Json;
using NL.Identity.Core;

namespace NL.Identity;

public sealed class JsonFileIdentityStore : IIdentityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _accountsPath;
    private readonly string _indexPath;
    private readonly object _lock = new();

    public JsonFileIdentityStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? NlIdentityPaths.Root;
        Directory.CreateDirectory(root);
        _accountsPath = Path.Combine(root, "accounts.json");
        _indexPath = Path.Combine(root, "platform-index.json");
    }

    public NlIdentityAccount? GetAccount(string accountId)
    {
        lock (_lock)
        {
            var db = LoadAccounts();
            return db.Accounts.TryGetValue(accountId, out var account) ? Clone(account) : null;
        }
    }

    public NlIdentityAccount? GetAccountByPlatformLink(NlPlatform platform, string externalUserId)
    {
        var accountId = GetAccountIdForPlatformLink(platform, externalUserId);
        return accountId is null ? null : GetAccount(accountId);
    }

    public string? GetAccountIdForPlatformLink(NlPlatform platform, string externalUserId)
    {
        lock (_lock)
        {
            var index = LoadIndex();
            var key = NlPlatformNames.LinkKey(platform, externalUserId);
            return index.TryGetValue(key, out var accountId) ? accountId : null;
        }
    }

    public void SaveAccount(NlIdentityAccount account)
    {
        lock (_lock)
        {
            var db = LoadAccounts();
            var index = LoadIndex();
            db.Accounts[account.Id] = Clone(account);
            RebuildIndex(db, index);
            SaveAccounts(db);
            SaveIndex(index);
        }
    }

    public void DeleteAccount(string accountId)
    {
        lock (_lock)
        {
            var db = LoadAccounts();
            if (!db.Accounts.Remove(accountId))
            {
                return;
            }

            var index = LoadIndex();
            RebuildIndex(db, index);
            SaveAccounts(db);
            SaveIndex(index);
        }
    }

    public IReadOnlyList<NlIdentityAccount> ListAccounts()
    {
        lock (_lock)
        {
            return LoadAccounts().Accounts.Values.Select(Clone).ToList();
        }
    }

    private static void RebuildIndex(AccountDatabase db, Dictionary<string, string> index)
    {
        index.Clear();
        foreach (var account in db.Accounts.Values)
        {
            foreach (var link in account.Links)
            {
                index[NlPlatformNames.LinkKey(link.Platform, link.ExternalUserId)] = account.Id;
            }
        }
    }

    private AccountDatabase LoadAccounts()
    {
        if (!File.Exists(_accountsPath))
        {
            return new AccountDatabase();
        }

        var json = File.ReadAllText(_accountsPath);
        return JsonSerializer.Deserialize<AccountDatabase>(json, JsonOptions) ?? new AccountDatabase();
    }

    private void SaveAccounts(AccountDatabase db) =>
        File.WriteAllText(_accountsPath, JsonSerializer.Serialize(db, JsonOptions));

    private Dictionary<string, string> LoadIndex()
    {
        if (!File.Exists(_indexPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(_indexPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SaveIndex(Dictionary<string, string> index) =>
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(index, JsonOptions));

    private static NlIdentityAccount Clone(NlIdentityAccount source) => new()
    {
        Id = source.Id,
        DisplayName = source.DisplayName,
        CreatedAtUtc = source.CreatedAtUtc,
        Links = source.Links.Select(l => new NlPlatformLink
        {
            Platform = l.Platform,
            ExternalUserId = l.ExternalUserId,
            LinkedAtUtc = l.LinkedAtUtc,
            ProtectedRefreshToken = l.ProtectedRefreshToken,
            TokenExpiresAtUtc = l.TokenExpiresAtUtc,
        }).ToList(),
    };

    private sealed class AccountDatabase
    {
        public Dictionary<string, NlIdentityAccount> Accounts { get; set; } = new(StringComparer.Ordinal);
    }
}

public static class NlIdentityPaths
{
    public static string Root
    {
        get
        {
            var overrideRoot = Environment.GetEnvironmentVariable("NL_IDENTITY_ROOT");
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return Path.GetFullPath(overrideRoot);
            }

            var dataRoot = Environment.GetEnvironmentVariable("NL_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(dataRoot))
            {
                return Path.Combine(Path.GetFullPath(dataRoot), "identity");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NL",
                "identity");
        }
    }

    public static string AuditLog => Path.Combine(Root, "identity-audit.jsonl");

    public static string MockOwnershipConfig => Path.Combine(Root, "mock-ownership.json");

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
