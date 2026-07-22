using System.Collections.Concurrent;

namespace NL.Server;

/// <summary>Simple sliding-window rate limiter for spectator trigger endpoints.</summary>
public sealed class NlSpectatorRateLimiter
{
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _buckets = new();

    public NlSpectatorRateLimiter(int maxPerMinute, TimeSpan? window = null)
    {
        _maxPerWindow = Math.Max(1, maxPerMinute);
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public bool TryAcquire(string key)
    {
        var now = DateTimeOffset.UtcNow;
        var queue = _buckets.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > _window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= _maxPerWindow)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}
