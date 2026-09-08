using System.Text.Json;
using NL.Identity.Core;

namespace NL.Identity;

public sealed class JsonFileIdentityStore : IIdentityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _accountsPath;
    private readonly string _indexPath;
    private readonly string _emailIndexPath;
    private readonly string _streamerIndexPath;
    private readonly object _lock = new();

    public JsonFileIdentityStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? NlIdentityPaths.Root;
        Directory.CreateDirectory(root);
        _accountsPath = Path.Combine(root, "accounts.json");
        _indexPath = Path.Combine(root, "platform-index.json");
        _emailIndexPath = Path.Combine(root, "email-index.json");
        _streamerIndexPath = Path.Combine(root, "streamer-index.json");
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

    public string? GetAccountIdForEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        lock (_lock)
        {
            var index = LoadEmailIndex();
            return index.TryGetValue(email.Trim().ToLowerInvariant(), out var accountId) ? accountId : null;
        }
    }

    public string? GetAccountIdForStreamer(string streamerId)
    {
        if (string.IsNullOrWhiteSpace(streamerId))
        {
            return null;
        }

        lock (_lock)
        {
            var index = LoadStreamerIndex();
            return index.TryGetValue(streamerId.Trim(), out var accountId) ? accountId : null;
        }
    }

    public void SaveAccount(NlIdentityAccount account)
    {
        lock (_lock)
        {
            var db = LoadAccounts();
            var platformIndex = LoadIndex();
            var emailIndex = LoadEmailIndex();
            var streamerIndex = LoadStreamerIndex();
            db.Accounts[account.Id] = Clone(account);
            RebuildIndexes(db, platformIndex, emailIndex, streamerIndex);
            SaveAccounts(db);
            SaveIndex(platformIndex);
            SaveEmailIndex(emailIndex);
            SaveStreamerIndex(streamerIndex);
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

            var platformIndex = LoadIndex();
            var emailIndex = LoadEmailIndex();
            var streamerIndex = LoadStreamerIndex();
            RebuildIndexes(db, platformIndex, emailIndex, streamerIndex);
            SaveAccounts(db);
            SaveIndex(platformIndex);
            SaveEmailIndex(emailIndex);
            SaveStreamerIndex(streamerIndex);
        }
    }

    public IReadOnlyList<NlIdentityAccount> ListAccounts()
    {
        lock (_lock)
        {
            return LoadAccounts().Accounts.Values.Select(Clone).ToList();
        }
    }

    private static void RebuildIndexes(
        AccountDatabase db,
        Dictionary<string, string> platformIndex,
        Dictionary<string, string> emailIndex,
        Dictionary<string, string> streamerIndex)
    {
        platformIndex.Clear();
        emailIndex.Clear();
        streamerIndex.Clear();

        foreach (var account in db.Accounts.Values)
        {
            foreach (var link in account.Links)
            {
                platformIndex[NlPlatformNames.LinkKey(link.Platform, link.ExternalUserId)] = account.Id;
            }

            if (!string.IsNullOrWhiteSpace(account.Email))
            {
                emailIndex[account.Email.Trim().ToLowerInvariant()] = account.Id;
            }

            if (!string.IsNullOrWhiteSpace(account.StreamerId))
            {
                streamerIndex[account.StreamerId.Trim()] = account.Id;
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

    private Dictionary<string, string> LoadEmailIndex()
    {
        if (!File.Exists(_emailIndexPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(_emailIndexPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SaveEmailIndex(Dictionary<string, string> index) =>
        File.WriteAllText(_emailIndexPath, JsonSerializer.Serialize(index, JsonOptions));

    private Dictionary<string, string> LoadStreamerIndex()
    {
        if (!File.Exists(_streamerIndexPath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var json = File.ReadAllText(_streamerIndexPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private void SaveStreamerIndex(Dictionary<string, string> index) =>
        File.WriteAllText(_streamerIndexPath, JsonSerializer.Serialize(index, JsonOptions));

    private static NlIdentityAccount Clone(NlIdentityAccount source) => new()
    {
        Id = source.Id,
        DisplayName = source.DisplayName,
        CreatedAtUtc = source.CreatedAtUtc,
        Email = source.Email,
        EmailVerifiedAtUtc = source.EmailVerifiedAtUtc,
        ProtectedTotpSecret = source.ProtectedTotpSecret,
        TwoFactorEnabledAtUtc = source.TwoFactorEnabledAtUtc,
        StreamerId = source.StreamerId,
        StreamerEnabledAtUtc = source.StreamerEnabledAtUtc,
        ProtectedPasswordHash = source.ProtectedPasswordHash,
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
