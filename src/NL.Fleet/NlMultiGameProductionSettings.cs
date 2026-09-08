namespace NL.Fleet;

public sealed class NlMultiGameProductionSettings
{
    public const string EnabledVariable = "NL_MULTIGAME_PRODUCTION_ENABLED";

    public bool Enabled { get; init; }

    public bool RequireLiveProduction { get; init; } = true;

    public bool RequirePlayerUx { get; init; } = true;

    public bool RequirePartnershipGate { get; init; } = true;

    public IReadOnlyList<string> RequiredGameIds { get; init; } = ["hello-fork", "minecraft", "beamng"];

    public static NlMultiGameProductionSettings LoadFromEnvironment()
    {
        var enabled = IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));
        var requireLive = !string.Equals(
            Environment.GetEnvironmentVariable("NL_MULTIGAME_REQUIRE_LIVE_PRODUCTION"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requirePlayer = !string.Equals(
            Environment.GetEnvironmentVariable("NL_MULTIGAME_REQUIRE_PLAYER_UX"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var requirePartnership = !string.Equals(
            Environment.GetEnvironmentVariable("NL_MULTIGAME_REQUIRE_PARTNERSHIP"),
            "0",
            StringComparison.OrdinalIgnoreCase);

        var required = (Environment.GetEnvironmentVariable("NL_MULTIGAME_REQUIRED_GAMES") ?? "hello-fork,minecraft,beamng")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToArray();

        return new NlMultiGameProductionSettings
        {
            Enabled = enabled,
            RequireLiveProduction = requireLive,
            RequirePlayerUx = requirePlayer,
            RequirePartnershipGate = requirePartnership,
            RequiredGameIds = required,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        requireLiveProduction = RequireLiveProduction,
        requirePlayerUx = RequirePlayerUx,
        requirePartnershipGate = RequirePartnershipGate,
        requiredGameIds = RequiredGameIds,
        opsPath = "/multigame-ops.html",
        playerPath = "/nl-client.html",
    };

    private static bool IsTruthy(string? raw) =>
        string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
}
