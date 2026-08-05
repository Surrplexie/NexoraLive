namespace NL.Fleet;

public sealed class NlProductionCutoverSettings
{
    public const string EnabledVariable = "NL_PRODUCTION_CUTOVER_ENABLED";

    /// <summary>Local validation: allow 127.0.0.1 URLs while verifying production flag config.</summary>
    public bool DevMode { get; init; }

    public bool Enabled { get; init; }

    public bool RequireLaunchOpsGate { get; init; } = true;

    public bool RequireMultiGameGate { get; init; } = true;

    public bool RequireLiveProductionGate { get; init; } = true;

    public bool RequirePublicHttpsProbe { get; init; } = true;

    public static NlProductionCutoverSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_PRODUCTION_CUTOVER_DEV"));

        var requireLaunch = !string.Equals(
            Environment.GetEnvironmentVariable("NL_CUTOVER_REQUIRE_LAUNCH_OPS"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireMulti = !string.Equals(
            Environment.GetEnvironmentVariable("NL_CUTOVER_REQUIRE_MULTIGAME"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireLive = !string.Equals(
            Environment.GetEnvironmentVariable("NL_CUTOVER_REQUIRE_LIVE_PRODUCTION"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireHttps = !string.Equals(
            Environment.GetEnvironmentVariable("NL_CUTOVER_REQUIRE_HTTPS_PROBE"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        return new NlProductionCutoverSettings
        {
            Enabled = enabled,
            DevMode = devMode,
            RequireLaunchOpsGate = requireLaunch,
            RequireMultiGameGate = requireMulti,
            RequireLiveProductionGate = requireLive,
            RequirePublicHttpsProbe = requireHttps,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        opsPath = "/production-cutover-ops.html",
        docsPath = "docs/NL_PRODUCTION_CUTOVER.md",
        requiredEnv = new[]
        {
            "NL_LIVE_PRODUCTION_DEV=false",
            "NL_LAUNCH_OPS_DEV=false",
            "NL_GA_ALLOW_MOCK_IDENTITY=false",
            "NL_GA_REQUIRE_PRODUCTION_READY=true",
            "NL_GA_REQUIRE_LIVE_IDENTITY=true",
        },
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
