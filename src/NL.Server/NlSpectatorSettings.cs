namespace NL.Server;

/// <summary>Phase H — public spectator UX (read-only feed + rate-limited demo triggers).</summary>
public sealed class NlSpectatorSettings
{
    public const string TriggersEnabledVariable = "NL_SPECTATOR_TRIGGERS";
    public const string TriggerRateLimitVariable = "NL_SPECTATOR_TRIGGER_RATE_PER_MIN";
    public const string FeedMaxCountVariable = "NL_SPECTATOR_FEED_MAX";

    public bool TriggersEnabled { get; init; } = true;

    public int TriggerRatePerMinute { get; init; } = 12;

    public int FeedMaxCount { get; init; } = 100;

    public static NlSpectatorSettings LoadFromEnvironment()
    {
        var triggersRaw = Environment.GetEnvironmentVariable(TriggersEnabledVariable);
        var triggersEnabled = triggersRaw is null
            || ParseBool(triggersRaw);

        var rate = ParseInt(Environment.GetEnvironmentVariable(TriggerRateLimitVariable), defaultValue: 12);
        var feedMax = ParseInt(Environment.GetEnvironmentVariable(FeedMaxCountVariable), defaultValue: 100);

        return new NlSpectatorSettings
        {
            TriggersEnabled = triggersEnabled,
            TriggerRatePerMinute = Math.Max(1, rate),
            FeedMaxCount = Math.Clamp(feedMax, 10, 500),
        };
    }

    private static bool ParseBool(string value) =>
        value.Equals("1", StringComparison.OrdinalIgnoreCase)
        || value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) ? parsed : defaultValue;
}
