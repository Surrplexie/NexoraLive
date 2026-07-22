using NL.Server.Core.Security;

namespace NL.Server;

/// <summary>HTTP rate-limit buckets for public demo endpoints (Phase K).</summary>
public sealed class NlPublicRateLimitService
{
    private readonly NlHardeningSettings _settings;
    private readonly NlSlidingWindowRateLimiter _admit;
    private readonly NlSlidingWindowRateLimiter _publicRead;

    public NlPublicRateLimitService(NlHardeningSettings settings)
    {
        _settings = settings;
        _admit = new NlSlidingWindowRateLimiter(settings.AdmitRatePerMinute);
        _publicRead = new NlSlidingWindowRateLimiter(settings.PublicReadRatePerMinute);
    }

    public bool IsEnabled => _settings.Enabled;

    public bool TryAdmit(string clientKey) =>
        !_settings.Enabled || _admit.TryAcquire(clientKey);

    public bool TryPublicRead(string clientKey) =>
        !_settings.Enabled || _publicRead.TryAcquire(clientKey);

    public object GetMetrics() => new
    {
        admitRejected = _admit.RejectedCount,
        publicReadRejected = _publicRead.RejectedCount,
    };
}
