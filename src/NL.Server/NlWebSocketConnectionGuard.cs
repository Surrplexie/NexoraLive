using System.Collections.Concurrent;
using NL.Server.Core.Security;

namespace NL.Server;

/// <summary>Caps WebSocket bridge connections and connect attempts (Phase K).</summary>
public sealed class NlWebSocketConnectionGuard
{
    private static NlWebSocketConnectionGuard? _current;

    private readonly NlHardeningSettings _settings;
    private readonly NlSlidingWindowRateLimiter _connectRate;
    private int _activeConnections;
    private readonly ConcurrentDictionary<string, int> _perIp = new();
    private long _rejected;

    public NlWebSocketConnectionGuard(NlHardeningSettings settings)
    {
        _settings = settings;
        _connectRate = new NlSlidingWindowRateLimiter(settings.WebSocketConnectRatePerMinute);
    }

    public static NlWebSocketConnectionGuard? Current => _current;

    public static void Configure(NlHardeningSettings settings)
    {
        _current = settings.Enabled ? new NlWebSocketConnectionGuard(settings) : null;
    }

    public int ActiveConnections => Volatile.Read(ref _activeConnections);

    public long RejectedCount => Interlocked.Read(ref _rejected);

    public bool TryAccept(string remoteIp, out string? reason)
    {
        reason = null;
        if (!_settings.Enabled)
        {
            return true;
        }

        var key = string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp;
        if (!_connectRate.TryAcquire(key))
        {
            reason = "WebSocket connect rate limit exceeded.";
            RecordReject();
            return false;
        }

        if (Volatile.Read(ref _activeConnections) >= _settings.WebSocketMaxConnections)
        {
            reason = "Maximum WebSocket bridge connections reached.";
            RecordReject();
            return false;
        }

        var ipCount = _perIp.AddOrUpdate(key, 1, static (_, c) => c + 1);
        if (ipCount > _settings.WebSocketMaxConnectionsPerIp)
        {
            _perIp.AddOrUpdate(key, 0, static (_, c) => Math.Max(0, c - 1));
            reason = "Maximum WebSocket connections per IP reached.";
            RecordReject();
            return false;
        }

        Interlocked.Increment(ref _activeConnections);
        return true;
    }

    public void Release(string remoteIp)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        Interlocked.Decrement(ref _activeConnections);
        var key = string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp;
        _perIp.AddOrUpdate(key, 0, static (_, c) => Math.Max(0, c - 1));
    }

    public object GetMetrics() => new
    {
        activeConnections = ActiveConnections,
        rejected = RejectedCount,
        connectRateRejected = _connectRate.RejectedCount,
        maxConnections = _settings.WebSocketMaxConnections,
        maxConnectionsPerIp = _settings.WebSocketMaxConnectionsPerIp,
    };

    private void RecordReject() => Interlocked.Increment(ref _rejected);
}
