namespace NL.Fleet;

public sealed class NlLiveProductionSettings
{
    public const string EnabledVariable = "NL_LIVE_PRODUCTION_ENABLED";

    public bool Enabled { get; init; }

    /// <summary>Local Docker validation: allow 127.0.0.1 URLs and mock identity for join smoke.</summary>
    public bool DevMode { get; init; }

    public static NlLiveProductionSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_LIVE_PRODUCTION_DEV"));

        return new NlLiveProductionSettings
        {
            Enabled = enabled,
            DevMode = devMode,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        opsPath = "/live-production-ops.html",
        docsPath = "docs/NL_LIVE_PRODUCTION.md",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
