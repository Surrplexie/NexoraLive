namespace NL.Fleet;

public sealed class NlBetaSettings
{
    public const string EnabledVariable = "NL_BETA_ENABLED";

    public bool Enabled { get; init; }

    public bool WaitlistOpen { get; init; } = true;

    public int MaxApprovedStreamers { get; init; } = 100;

    public bool EnforceStreamerAllowlist { get; init; } = true;

    /// <summary>Local beta validation may use mock identity when Steam key is absent.</summary>
    public bool AllowMockIdentity { get; init; }

    public bool RequireProductionReady { get; init; } = true;

    public IReadOnlyList<string> OperatorStreamers { get; init; } = Array.Empty<string>();

    public static NlBetaSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var waitlistOpen = !string.Equals(
            Environment.GetEnvironmentVariable("NL_BETA_WAITLIST_OPEN"),
            "0",
            StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Environment.GetEnvironmentVariable("NL_BETA_WAITLIST_OPEN"),
                "false",
                StringComparison.OrdinalIgnoreCase);

        var max = int.TryParse(Environment.GetEnvironmentVariable("NL_BETA_MAX_STREAMERS"), out var m)
            ? Math.Max(1, m)
            : 100;

        var enforce = !string.Equals(
            Environment.GetEnvironmentVariable("NL_BETA_ENFORCE_ALLOWLIST"),
            "0",
            StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Environment.GetEnvironmentVariable("NL_BETA_ENFORCE_ALLOWLIST"),
                "false",
                StringComparison.OrdinalIgnoreCase);

        var allowMock = IsTruthy(Environment.GetEnvironmentVariable("NL_BETA_ALLOW_MOCK_IDENTITY"));

        var requireProduction = !string.Equals(
            Environment.GetEnvironmentVariable("NL_BETA_REQUIRE_PRODUCTION_READY"),
            "0",
            StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Environment.GetEnvironmentVariable("NL_BETA_REQUIRE_PRODUCTION_READY"),
                "false",
                StringComparison.OrdinalIgnoreCase);

        var operators = (Environment.GetEnvironmentVariable("NL_BETA_OPERATOR_STREAMERS") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToArray();

        return new NlBetaSettings
        {
            Enabled = enabled,
            WaitlistOpen = waitlistOpen,
            MaxApprovedStreamers = max,
            EnforceStreamerAllowlist = enforce,
            AllowMockIdentity = allowMock,
            RequireProductionReady = requireProduction,
            OperatorStreamers = operators,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        waitlistOpen = WaitlistOpen,
        maxApprovedStreamers = MaxApprovedStreamers,
        enforceStreamerAllowlist = EnforceStreamerAllowlist,
        allowMockIdentity = AllowMockIdentity,
        signupPath = "/beta.html",
        opsPath = "/beta-ops.html",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
