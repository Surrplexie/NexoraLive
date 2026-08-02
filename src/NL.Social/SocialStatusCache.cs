using NL.Social.Core;

namespace NL.Social;

public sealed class SocialStatusCache
{
    private readonly TimeSpan _ttl;
    private readonly object _lock = new();
    private readonly Dictionary<string, CacheEntry> _relationships = new();
    private readonly Dictionary<string, CacheEntryLive> _live = new();

    public SocialStatusCache(TimeSpan ttl) => _ttl = ttl;

    public bool TryGetRelationship(string key, out SocialRelationshipStatus status)
    {
        lock (_lock)
        {
            if (_relationships.TryGetValue(key, out var entry) && !entry.IsExpired(_ttl))
            {
                status = entry.Value;
                return true;
            }
        }

        status = SocialRelationshipStatus.Unknown;
        return false;
    }

    public void SetRelationship(string key, SocialRelationshipStatus status)
    {
        lock (_lock)
        {
            _relationships[key] = new CacheEntry(status, DateTimeOffset.UtcNow);
        }
    }

    public bool TryGetLive(string streamerId, out LiveStreamStatus status)
    {
        lock (_lock)
        {
            if (_live.TryGetValue(streamerId, out var entry) && !entry.IsExpired(_ttl))
            {
                status = entry.Value;
                return true;
            }
        }

        status = new LiveStreamStatus(false);
        return false;
    }

    public void SetLive(string streamerId, LiveStreamStatus status)
    {
        lock (_lock)
        {
            _live[streamerId] = new CacheEntryLive(status, DateTimeOffset.UtcNow);
        }
    }

    public void InvalidateAll()
    {
        lock (_lock)
        {
            _relationships.Clear();
            _live.Clear();
        }
    }

    private readonly record struct CacheEntry(SocialRelationshipStatus Value, DateTimeOffset StoredAtUtc)
    {
        public bool IsExpired(TimeSpan ttl) => DateTimeOffset.UtcNow - StoredAtUtc > ttl;
    }

    private readonly record struct CacheEntryLive(LiveStreamStatus Value, DateTimeOffset StoredAtUtc)
    {
        public bool IsExpired(TimeSpan ttl) => DateTimeOffset.UtcNow - StoredAtUtc > ttl;
    }
}
