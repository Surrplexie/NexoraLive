using System.Text.Json;

namespace NL.Identity;

public sealed record OAuthStateEntry(
    string State,
    string AccountId,
    string? ReturnUrl,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Short-lived CSRF state for browser OAuth / OpenID redirects.</summary>
public sealed class JsonOAuthStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, OAuthStateEntry> _states = new(StringComparer.Ordinal);

    public JsonOAuthStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(NlIdentityPaths.Root, "oauth-state.json");
        Load();
    }

    public string Create(string accountId, string? returnUrl, TimeSpan ttl)
    {
        var state = Guid.NewGuid().ToString("N");
        var entry = new OAuthStateEntry(state, accountId.Trim(), returnUrl, DateTimeOffset.UtcNow.Add(ttl));
        lock (_lock)
        {
            PurgeExpired_NoLock();
            _states[state] = entry;
            Persist_NoLock();
        }

        return state;
    }

    public OAuthStateEntry? Consume(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        lock (_lock)
        {
            PurgeExpired_NoLock();
            if (!_states.TryGetValue(state.Trim(), out var entry))
            {
                return null;
            }

            _states.Remove(state.Trim());
            Persist_NoLock();
            return entry.ExpiresAtUtc >= DateTimeOffset.UtcNow ? entry : null;
        }
    }

    private void PurgeExpired_NoLock()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _states.Where(kv => kv.Value.ExpiresAtUtc < now).Select(kv => kv.Key).ToList())
        {
            _states.Remove(key);
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
            var list = JsonSerializer.Deserialize<List<OAuthStateEntry>>(File.ReadAllText(_path), JsonOptions);
            if (list is null)
            {
                return;
            }

            _states = list.ToDictionary(e => e.State, StringComparer.Ordinal);
            PurgeExpired_NoLock();
        }
        catch
        {
            _states = new Dictionary<string, OAuthStateEntry>(StringComparer.Ordinal);
        }
    }

    private void Persist_NoLock()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_states.Values.ToList(), JsonOptions));
    }
}
