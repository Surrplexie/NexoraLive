namespace NL.Server;

/// <summary>Process uptime and ops counters for monitoring (Phase K).</summary>
public static class NlOpsMetrics
{
    private static readonly DateTimeOffset StartedUtc = DateTimeOffset.UtcNow;

    public static TimeSpan Uptime => DateTimeOffset.UtcNow - StartedUtc;

    public static object UptimePayload() => new
    {
        startedUtc = StartedUtc,
        uptimeSeconds = (long)Uptime.TotalSeconds,
    };
}
