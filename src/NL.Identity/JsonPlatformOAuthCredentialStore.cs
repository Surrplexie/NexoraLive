using System.Text.Json;
using NL.Identity.Core;

namespace NL.Identity;

public sealed class JsonPlatformOAuthCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, PlatformOAuthCredential> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _accountByPlatformUser = new(StringComparer.OrdinalIgnoreCase);

    public JsonPlatformOAuthCredentialStore(string? path = null)
    {
        _path = path ?? Path.Combine(NlIdentityPaths.Root, "platform-oauth-credentials.json");
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public PlatformOAuthCredential? Get(NlPlatform platform, string accountId)
    {
        lock (_lock)
        {
            return _byKey.GetValueOrDefault(Key(platform, accountId));
        }
    }

    public PlatformOAuthCredential? GetByPlatformUser(NlPlatform platform, string externalUserId)
    {
        lock (_lock)
        {
            var mapKey = PlatformUserKey(platform, externalUserId);
            return _accountByPlatformUser.TryGetValue(mapKey, out var accountId)
                ? _byKey.GetValueOrDefault(Key(platform, accountId))
                : null;
        }
    }

    public PlatformOAuthCredential Save(PlatformOAuthCredential credential)
    {
        var accountId = credential.AccountId.Trim();
        var externalUserId = credential.ExternalUserId.Trim();
        var platformUserKey = PlatformUserKey(credential.Platform, externalUserId);

        lock (_lock)
        {
            if (_accountByPlatformUser.TryGetValue(platformUserKey, out var existingAccount)
                && !string.Equals(existingAccount, accountId, StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformLinkConflictException(
                    NlPlatformNames.LinkKey(credential.Platform, externalUserId),
                    existingAccount);
            }

            var storeKey = Key(credential.Platform, accountId);
            if (_byKey.TryGetValue(storeKey, out var previous)
                && !string.Equals(previous.ExternalUserId, externalUserId, StringComparison.Ordinal))
            {
                _accountByPlatformUser.Remove(PlatformUserKey(credential.Platform, previous.ExternalUserId));
            }

            var normalized = credential with { AccountId = accountId, ExternalUserId = externalUserId };
            _byKey[storeKey] = normalized;
            _accountByPlatformUser[platformUserKey] = accountId;
            File.WriteAllText(_path, JsonSerializer.Serialize(_byKey.Values.ToList(), JsonOptions));
            return normalized;
        }
    }

    private static string Key(NlPlatform platform, string accountId) =>
        $"{NlPlatformNames.Normalize(platform)}:{accountId}";

    private static string PlatformUserKey(NlPlatform platform, string externalUserId) =>
        $"{NlPlatformNames.Normalize(platform)}:{externalUserId.Trim()}";

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<PlatformOAuthCredential>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _byKey = list.ToDictionary(c => Key(c.Platform, c.AccountId), StringComparer.OrdinalIgnoreCase);
            _accountByPlatformUser = list.ToDictionary(
                c => PlatformUserKey(c.Platform, c.ExternalUserId),
                c => c.AccountId,
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _byKey = new Dictionary<string, PlatformOAuthCredential>(StringComparer.OrdinalIgnoreCase);
            _accountByPlatformUser = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
