using System.Text.Json;
using NL.Identity.Core;

namespace NL.Identity;

public sealed class JsonLoginSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, NlLoginSession> _byToken = new(StringComparer.Ordinal);

    public JsonLoginSessionStore(string? path = null)
    {
        _path = path ?? Path.Combine(NlIdentityPaths.Root, "login-sessions.json");
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public NlLoginSession Create(string accountId, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var session = new NlLoginSession(
            token,
            accountId.Trim(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.Add(ttl));

        lock (_lock)
        {
            PurgeExpired_NoLock();
            _byToken[token] = session;
            Persist_NoLock();
        }

        return session;
    }

    public NlLoginSession? GetValid(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        lock (_lock)
        {
            PurgeExpired_NoLock();
            if (!_byToken.TryGetValue(token.Trim(), out var session))
            {
                return null;
            }

            return session.ExpiresAtUtc >= DateTimeOffset.UtcNow ? session : null;
        }
    }

    public bool Revoke(string token)
    {
        lock (_lock)
        {
            var removed = _byToken.Remove(token.Trim());
            if (removed)
            {
                Persist_NoLock();
            }

            return removed;
        }
    }

    public void RevokeAllForAccount(string accountId)
    {
        lock (_lock)
        {
            foreach (var key in _byToken.Where(kv => string.Equals(kv.Value.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key).ToList())
            {
                _byToken.Remove(key);
            }

            Persist_NoLock();
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
            var list = JsonSerializer.Deserialize<List<NlLoginSession>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _byToken = list.ToDictionary(s => s.Token, StringComparer.Ordinal);
            PurgeExpired_NoLock();
        }
        catch
        {
            _byToken = new Dictionary<string, NlLoginSession>(StringComparer.Ordinal);
        }
    }

    private void PurgeExpired_NoLock()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _byToken.Where(kv => kv.Value.ExpiresAtUtc < now).Select(kv => kv.Key).ToList())
        {
            _byToken.Remove(key);
        }
    }

    private void Persist_NoLock() =>
        File.WriteAllText(_path, JsonSerializer.Serialize(_byToken.Values.ToList(), JsonOptions));
}
