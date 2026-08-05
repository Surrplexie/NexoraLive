namespace NL.Fleet;

public sealed class NlDistributionSettings
{
    public const string EnabledVariable = "NL_DISTRIBUTION_ENABLED";

    public bool Enabled { get; init; }

    public bool DevMode { get; init; }

    public bool RequireProductionCutover { get; init; } = true;

    public bool RequireClientPackage { get; init; } = true;

    public bool RequireStreamerSignupSmoke { get; init; } = true;

    public bool RequirePlayerJoinSmoke { get; init; } = true;

    public string ClientVersion { get; init; } = "1.0.0";

    public string WinPackageRelativePath { get; init; } = "downloads/nl-client-win-x64.zip";

    public string DeepLinkScheme { get; init; } = "nlclient";

    public static NlDistributionSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_DISTRIBUTION_DEV"));

        var requireCutover = !string.Equals(
            Environment.GetEnvironmentVariable("NL_DISTRIBUTION_REQUIRE_CUTOVER"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requirePackage = !string.Equals(
            Environment.GetEnvironmentVariable("NL_DISTRIBUTION_REQUIRE_CLIENT_PACKAGE"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireStreamer = !string.Equals(
            Environment.GetEnvironmentVariable("NL_DISTRIBUTION_REQUIRE_STREAMER_SMOKE"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requirePlayer = !string.Equals(
            Environment.GetEnvironmentVariable("NL_DISTRIBUTION_REQUIRE_PLAYER_SMOKE"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        return new NlDistributionSettings
        {
            Enabled = enabled,
            DevMode = devMode,
            RequireProductionCutover = requireCutover,
            RequireClientPackage = requirePackage,
            RequireStreamerSignupSmoke = requireStreamer,
            RequirePlayerJoinSmoke = requirePlayer,
            ClientVersion = Environment.GetEnvironmentVariable("NL_DISTRIBUTION_CLIENT_VERSION") ?? "1.0.0",
            WinPackageRelativePath = Environment.GetEnvironmentVariable("NL_DISTRIBUTION_WIN_PACKAGE")
                ?? "downloads/nl-client-win-x64.zip",
            DeepLinkScheme = Environment.GetEnvironmentVariable("NL_DISTRIBUTION_DEEPLINK_SCHEME") ?? "nlclient",
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        clientVersion = ClientVersion,
        landingPath = "/play.html",
        downloadPath = "/download.html",
        webClientPath = "/nl-client.html",
        streamerSignupPath = "/ga.html",
        opsPath = "/distribution-ops.html",
        deepLinkScheme = DeepLinkScheme,
        clientManifestPath = "/api/v1/distribution/client-manifest",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
