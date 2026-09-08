using System.Text.Json;

namespace NL.Identity;

public sealed record EmailVerificationChallenge(
    string AccountId,
    string Email,
    string Code,
    DateTimeOffset ExpiresAtUtc);

public sealed class JsonEmailVerificationChallengeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, EmailVerificationChallenge> _byAccount = new(StringComparer.OrdinalIgnoreCase);

    public JsonEmailVerificationChallengeStore(string? path = null)
    {
        _path = path ?? Path.Combine(NlIdentityPaths.Root, "email-verification-challenges.json");
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public EmailVerificationChallenge Save(EmailVerificationChallenge challenge)
    {
        lock (_lock)
        {
            _byAccount[challenge.AccountId.Trim()] = challenge;
            Persist_NoLock();
            return challenge;
        }
    }

    public EmailVerificationChallenge? Consume(string accountId, string code)
    {
        lock (_lock)
        {
            if (!_byAccount.TryGetValue(accountId.Trim(), out var challenge))
            {
                return null;
            }

            if (!string.Equals(challenge.Code, code.Trim(), StringComparison.Ordinal)
                || challenge.ExpiresAtUtc < DateTimeOffset.UtcNow)
            {
                return null;
            }

            _byAccount.Remove(accountId.Trim());
            Persist_NoLock();
            return challenge;
        }
    }

    public EmailVerificationChallenge? Peek(string accountId)
    {
        lock (_lock)
        {
            return _byAccount.GetValueOrDefault(accountId.Trim());
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<EmailVerificationChallenge>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _byAccount = list.ToDictionary(c => c.AccountId, StringComparer.OrdinalIgnoreCase);
            PurgeExpired_NoLock();
        }
        catch
        {
            _byAccount = new Dictionary<string, EmailVerificationChallenge>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void PurgeExpired_NoLock()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _byAccount.Where(kv => kv.Value.ExpiresAtUtc < now).Select(kv => kv.Key).ToList())
        {
            _byAccount.Remove(key);
        }
    }

    private void Persist_NoLock()
    {
        PurgeExpired_NoLock();
        File.WriteAllText(_path, JsonSerializer.Serialize(_byAccount.Values.ToList(), JsonOptions));
    }
}
