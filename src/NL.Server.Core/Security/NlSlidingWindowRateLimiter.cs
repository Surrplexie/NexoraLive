using System.Collections.Concurrent;

namespace NL.Server.Core.Security;

/// <summary>Sliding-window rate limiter keyed by client identifier (typically IP).</summary>
public sealed class NlSlidingWindowRateLimiter
{
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _buckets = new();
    private long _rejected;

    public NlSlidingWindowRateLimiter(int maxPerWindow, TimeSpan? window = null)
    {
        _maxPerWindow = Math.Max(1, maxPerWindow);
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public long RejectedCount => Interlocked.Read(ref _rejected);

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
                Interlocked.Increment(ref _rejected);
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}
