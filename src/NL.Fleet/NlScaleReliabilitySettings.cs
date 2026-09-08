namespace NL.Fleet;

public sealed class NlScaleReliabilitySettings
{
    public const string EnabledVariable = "NL_SCALE_RELIABILITY_ENABLED";

    public bool Enabled { get; init; }

    public bool DevMode { get; init; }

    public bool RequireDistribution { get; init; } = true;

    public bool RequireLoadTest { get; init; } = true;

    public bool RequireMultiRegion { get; init; } = true;

    public int MinConcurrentSessions { get; init; } = 128;

    public int MinRegions { get; init; } = 3;

    public static NlScaleReliabilitySettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_SCALE_RELIABILITY_DEV"));

        var requireDistribution = !string.Equals(
            Environment.GetEnvironmentVariable("NL_SCALE_RELIABILITY_REQUIRE_DISTRIBUTION"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireLoadTest = !string.Equals(
            Environment.GetEnvironmentVariable("NL_SCALE_RELIABILITY_REQUIRE_LOAD_TEST"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireMultiRegion = !string.Equals(
            Environment.GetEnvironmentVariable("NL_SCALE_RELIABILITY_REQUIRE_MULTI_REGION"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var minConcurrent = int.TryParse(
            Environment.GetEnvironmentVariable("NL_SCALE_RELIABILITY_MIN_CONCURRENT"),
            out var mc)
            ? Math.Max(1, mc)
            : 128;

        var minRegions = int.TryParse(
            Environment.GetEnvironmentVariable("NL_SCALE_RELIABILITY_MIN_REGIONS"),
            out var mr)
            ? Math.Max(1, mr)
            : 3;

        return new NlScaleReliabilitySettings
        {
            Enabled = enabled,
            DevMode = devMode,
            RequireDistribution = requireDistribution,
            RequireLoadTest = requireLoadTest,
            RequireMultiRegion = requireMultiRegion,
            MinConcurrentSessions = minConcurrent,
            MinRegions = minRegions,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        minConcurrentSessions = MinConcurrentSessions,
        minRegions = MinRegions,
        opsPath = "/scale-reliability-ops.html",
        regionsPath = "/api/v1/scale-reliability/regions",
        productionSlosPath = "/api/v1/scale-reliability/production-slos",
        loadTestPath = "/api/v1/fleet/load-test/report",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
