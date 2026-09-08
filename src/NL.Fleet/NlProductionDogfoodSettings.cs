namespace NL.Fleet;

public sealed class NlProductionDogfoodSettings
{
    public const string EnabledVariable = "NL_PRODUCTION_DOGFOOD_ENABLED";

    public bool Enabled { get; init; }

    public bool DevMode { get; init; }

    public bool RequirePublicGaLaunch { get; init; } = true;

    public bool RequireDockerProvisioner { get; init; } = true;

    public bool RequireStreamerSignup { get; init; } = true;

    public bool RequireIdentityAccount { get; init; } = true;

    public bool RequirePlayerJoin { get; init; } = true;

    public bool RequireMultiGameSmokes { get; init; }

    public IReadOnlyList<string> RequiredGames { get; init; } = ["hello-fork"];

    public static NlProductionDogfoodSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var devMode = IsTruthy(Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_DEV"));

        var requireLaunch = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_REQUIRE_GA_LAUNCH"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireDocker = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_REQUIRE_DOCKER"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireSignup = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_REQUIRE_STREAMER_SIGNUP"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireIdentity = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_REQUIRE_IDENTITY"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireJoin = !string.Equals(
            Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_REQUIRE_PLAYER_JOIN"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requireMulti = IsTruthy(Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_REQUIRE_MULTIGAME"));

        var gamesRaw = Environment.GetEnvironmentVariable("NL_PRODUCTION_DOGFOOD_REQUIRED_GAMES")
            ?? "hello-fork";
        var games = gamesRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(g => g.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (games.Count == 0)
        {
            games.Add("hello-fork");
        }

        return new NlProductionDogfoodSettings
        {
            Enabled = enabled,
            DevMode = devMode,
            RequirePublicGaLaunch = requireLaunch,
            RequireDockerProvisioner = requireDocker,
            RequireStreamerSignup = requireSignup,
            RequireIdentityAccount = requireIdentity,
            RequirePlayerJoin = requireJoin,
            RequireMultiGameSmokes = requireMulti,
            RequiredGames = games,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        devMode = DevMode,
        requiredGames = RequiredGames,
        opsPath = "/production-dogfood-ops.html",
        validationPath = "/api/v1/production-dogfood/validation",
        docsPath = "docs/NL_PRODUCTION_DOGFOOD.md",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
