namespace NL.Server;

/// <summary>Phase G — hosted public demo loop (auto-start session + periodic reset).</summary>
public sealed class NlDemoSettings
{
    public const string DemoModeVariable = "NL_DEMO_MODE";
    public const string ResetIntervalVariable = "NL_DEMO_RESET_INTERVAL_MINUTES";
    public const string ConfigVariable = "NL_DEMO_CONFIG";
    public const string StartupDelayVariable = "NL_DEMO_STARTUP_DELAY_MS";

    public bool Enabled { get; init; }

    /// <summary>Zero disables periodic reset (startup reset still runs once).</summary>
    public TimeSpan ResetInterval { get; init; } = TimeSpan.FromMinutes(60);

    public string ConfigFileName { get; init; } = "demo.nle";

    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromMilliseconds(750);

    public static NlDemoSettings LoadFromEnvironment()
    {
        var enabled = ParseBool(Environment.GetEnvironmentVariable(DemoModeVariable));
        var intervalMinutes = ParseInt(Environment.GetEnvironmentVariable(ResetIntervalVariable), defaultValue: 60);
        var config = NullIfEmpty(Environment.GetEnvironmentVariable(ConfigVariable));
        var startupMs = ParseInt(Environment.GetEnvironmentVariable(StartupDelayVariable), defaultValue: 750);

        return new NlDemoSettings
        {
            Enabled = enabled,
            ResetInterval = intervalMinutes > 0 ? TimeSpan.FromMinutes(intervalMinutes) : TimeSpan.Zero,
            ConfigFileName = config ?? "demo.nle",
            StartupDelay = TimeSpan.FromMilliseconds(Math.Max(0, startupMs)),
        };
    }

    public object ToPublicInfo(bool sessionRunning, int decisions, string? configPath) => new
    {
        enabled = Enabled,
        sessionRunning,
        decisions,
        resetIntervalMinutes = ResetInterval.TotalMinutes,
        configPath,
        bridgeSidecarExpected = Enabled,
    };

    private static bool ParseBool(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static int ParseInt(string? value, int defaultValue) =>
        int.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
