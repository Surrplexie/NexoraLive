namespace NL.Fleet;

public sealed class NlGaSettings
{
    public const string EnabledVariable = "NL_GA_ENABLED";

    public bool Enabled { get; init; }

    /// <summary>Any streamer may register and host without beta waitlist approval.</summary>
    public bool OpenSignup { get; init; } = true;

    public bool RequireLiveIdentity { get; init; } = true;

    /// <summary>Local GA validation may use mock identity when Steam key is absent.</summary>
    public bool AllowMockIdentity { get; init; }

    public bool RequireProductionReady { get; init; } = true;

    /// <summary>GA requires beta waitlist program to be disabled.</summary>
    public bool RequireBetaDisabled { get; init; } = true;

    public int MinCatalogGames { get; init; } = 3;

    public IReadOnlyList<string> RequiredGameIds { get; init; } = ["hello-fork", "minecraft", "beamng"];

    public string SlaTier { get; init; } = "standard";

    public static NlGaSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var openSignup = !string.Equals(
            Environment.GetEnvironmentVariable("NL_GA_OPEN_SIGNUP"),
            "0",
            StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Environment.GetEnvironmentVariable("NL_GA_OPEN_SIGNUP"),
                "false",
                StringComparison.OrdinalIgnoreCase);

        var requireLive = !string.Equals(
            Environment.GetEnvironmentVariable("NL_GA_REQUIRE_LIVE_IDENTITY"),
            "0",
            StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Environment.GetEnvironmentVariable("NL_GA_REQUIRE_LIVE_IDENTITY"),
                "false",
                StringComparison.OrdinalIgnoreCase);

        var allowMock = IsTruthy(Environment.GetEnvironmentVariable("NL_GA_ALLOW_MOCK_IDENTITY"));

        var requireProduction = !string.Equals(
            Environment.GetEnvironmentVariable("NL_GA_REQUIRE_PRODUCTION_READY"),
            "0",
            StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Environment.GetEnvironmentVariable("NL_GA_REQUIRE_PRODUCTION_READY"),
                "false",
                StringComparison.OrdinalIgnoreCase);

        var requireBetaDisabled = !string.Equals(
            Environment.GetEnvironmentVariable("NL_GA_REQUIRE_BETA_DISABLED"),
            "0",
            StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Environment.GetEnvironmentVariable("NL_GA_REQUIRE_BETA_DISABLED"),
                "false",
                StringComparison.OrdinalIgnoreCase);

        var minGames = int.TryParse(Environment.GetEnvironmentVariable("NL_GA_MIN_CATALOG_GAMES"), out var min)
            ? Math.Max(1, min)
            : 3;

        var requiredGames = (Environment.GetEnvironmentVariable("NL_GA_REQUIRED_GAMES") ?? "hello-fork,minecraft,beamng")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToArray();

        var slaTier = Environment.GetEnvironmentVariable("NL_GA_SLA_TIER")?.Trim();
        if (string.IsNullOrWhiteSpace(slaTier))
        {
            slaTier = "standard";
        }

        return new NlGaSettings
        {
            Enabled = enabled,
            OpenSignup = openSignup,
            RequireLiveIdentity = requireLive,
            AllowMockIdentity = allowMock,
            RequireProductionReady = requireProduction,
            RequireBetaDisabled = requireBetaDisabled,
            MinCatalogGames = minGames,
            RequiredGameIds = requiredGames,
            SlaTier = slaTier,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        openSignup = OpenSignup,
        requireLiveIdentity = RequireLiveIdentity,
        allowMockIdentity = AllowMockIdentity,
        minCatalogGames = MinCatalogGames,
        requiredGameIds = RequiredGameIds,
        slaTier = SlaTier,
        signupPath = "/ga.html",
        opsPath = "/ga-ops.html",
        catalogPath = "/fork-catalog.html",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
