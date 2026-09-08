using NL.Server.Core.Security;

namespace NL.Server;

/// <summary>Rate limiter alias for spectator triggers (uses shared sliding-window limiter).</summary>
public sealed class NlSpectatorRateLimiter
{
    private readonly NlSlidingWindowRateLimiter _inner;

    public NlSpectatorRateLimiter(int maxPerMinute, TimeSpan? window = null) =>
        _inner = new NlSlidingWindowRateLimiter(maxPerMinute, window);

    public bool TryAcquire(string key) => _inner.TryAcquire(key);
}
