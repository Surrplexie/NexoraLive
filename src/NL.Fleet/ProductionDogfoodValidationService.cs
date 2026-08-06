using NL.Fleet.Core;

namespace NL.Fleet;

/// <summary>Production dogfood — end-to-end stream on real Docker fork images.</summary>
public sealed class ProductionDogfoodValidationService
{
    public ProductionDogfoodValidationReport Evaluate(
        NlProductionDogfoodSettings dogfood,
        NlPublicGaLaunchSettings launch,
        NlGaSettings ga,
        NlDistributionSettings distribution,
        bool identityEnabled,
        bool forkOrchestratorEnabled,
        string orchestratorMode,
        bool streamerSignupVerified,
        bool identityAccountVerified,
        bool playerJoinVerified,
        bool minecraftJoinVerified,
        bool beamngJoinVerified,
        bool forkTeardownVerified)
    {
        var dockerMode = string.Equals(orchestratorMode, "Docker", StringComparison.OrdinalIgnoreCase);
        var requiresMinecraft = dogfood.RequiredGames.Any(g =>
            string.Equals(g, "minecraft", StringComparison.OrdinalIgnoreCase));
        var requiresBeamng = dogfood.RequiredGames.Any(g =>
            string.Equals(g, "beamng", StringComparison.OrdinalIgnoreCase));

        var checks = new List<ProductionDogfoodValidationCheck>
        {
            Check("production_dogfood_enabled", "Production dogfood program enabled", dogfood.Enabled),
            Check(
                "public_ga_launch_program",
                "Public GA launch program enabled (upstream)",
                !dogfood.RequirePublicGaLaunch || launch.Enabled || dogfood.DevMode),
            Check(
                "distribution_program",
                "Distribution program enabled",
                distribution.Enabled || dogfood.DevMode),
            Check(
                "ga_open_signup",
                "Streamer signup enabled",
                ga.Enabled && ga.OpenSignup),
            Check(
                "identity_service",
                "Identity service enabled",
                identityEnabled),
            Check(
                "fork_orchestrator",
                "Fork orchestrator enabled",
                forkOrchestratorEnabled),
            Check(
                "docker_provisioner",
                "Fork orchestrator uses Docker provisioner",
                !dogfood.RequireDockerProvisioner || dockerMode || dogfood.DevMode,
                $"mode={orchestratorMode}"),
            Check(
                "streamer_signup_smoke",
                "Streamer GA registration smoke verified",
                !dogfood.RequireStreamerSignup || streamerSignupVerified || dogfood.DevMode),
            Check(
                "identity_account_smoke",
                "NL identity account create + Steam link verified",
                !dogfood.RequireIdentityAccount || identityAccountVerified || dogfood.DevMode),
            Check(
                "hello_fork_join_smoke",
                "Player join on hello-fork Docker fork verified",
                !dogfood.RequirePlayerJoin || playerJoinVerified || dogfood.DevMode,
                "hello-fork"),
            Check(
                "fork_teardown_smoke",
                "Fork destroyed after session stop verified",
                !dogfood.RequirePlayerJoin || forkTeardownVerified || dogfood.DevMode),
        };

        if (dogfood.RequireMultiGameSmokes || requiresMinecraft)
        {
            checks.Add(Check(
                "minecraft_join_smoke",
                "Player join on minecraft Docker fork verified",
                minecraftJoinVerified || dogfood.DevMode,
                "minecraft"));
        }

        if (dogfood.RequireMultiGameSmokes || requiresBeamng)
        {
            checks.Add(Check(
                "beamng_join_smoke",
                "Player join on beamng Docker fork verified",
                beamngJoinVerified || dogfood.DevMode,
                "beamng"));
        }

        var passed = checks.All(c => c.Passed);
        return new ProductionDogfoodValidationReport(passed, checks, DateTimeOffset.UtcNow);
    }

    private static ProductionDogfoodValidationCheck Check(
        string id,
        string description,
        bool passed,
        string? detail = null) =>
        new(id, description, passed, detail);
}
