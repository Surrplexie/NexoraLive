namespace NL.Fork.Core;

/// <summary>
/// Phase T0 — standard per-game fork adapter contract.
/// Describe a title once (events, actions, connect URL, health, image paths) so onboarding
/// does not require bespoke <c>Program.cs</c> / SessionHost edits.
/// Runtime enforcement still implements <see cref="IForkRuntime"/> (or a bridge sidecar).
/// </summary>
public interface IGameForkAdapter
{
    /// <summary>Stable catalog / orchestrator id (e.g. <c>rimworld</c>, <c>hello-fork</c>).</summary>
    string GameId { get; }

    string DisplayName { get; }

    /// <summary>Major-only version string for catalog rows (e.g. <c>1.0</c>).</summary>
    string MajorVersion { get; }

    /// <summary>Connect URL scheme for NL Client manifests (<c>minecraft</c>, <c>rimworld</c>, …).</summary>
    string ConnectScheme { get; }

    /// <summary>Optional native player connect port (e.g. 25565 for Minecraft).</summary>
    int? PlayerConnectPort { get; }

    string DockerImage { get; }

    /// <summary>Repo-relative Dockerfile used by <c>scripts/build-fork-images.ps1</c>.</summary>
    string Dockerfile { get; }

    /// <summary>Repo-relative default <c>.nle</c> template.</summary>
    string DefaultNleTemplate { get; }

    /// <summary>Repo-relative integration folder (<c>integrations/&lt;game&gt;/</c>).</summary>
    string IntegrationDir { get; }

    /// <summary>Repo-relative dogfood script (or shared <c>nl-dogfood-flow.ps1</c> with -GameId).</summary>
    string DogfoodScript { get; }

    /// <summary>Key accepted by <c>build-fork-images.ps1 -Images</c>.</summary>
    string BuildImageKey { get; }

    /// <summary>Events the adapter must emit (NL Integration Spec v1 names).</summary>
    IReadOnlyList<string> RequiredEvents { get; }

    /// <summary>Standard action verbs the adapter must handle (<c>warn</c>, <c>kick</c>, …).</summary>
    IReadOnlyList<string> RequiredActions { get; }

    GameForkHealthProbe Health { get; }

    IReadOnlyList<string> SteamAppIds { get; }

    /// <summary>Maps this adapter into an orchestrator <see cref="ForkGameProfile"/>.</summary>
    ForkGameProfile ToForkGameProfile();

    /// <summary>Orchestrator / operator readiness check (file status or HTTP).</summary>
    Task<GameForkHealthResult> ProbeHealthAsync(
        GameForkHealthContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Health probe configuration for a game fork adapter.</summary>
public sealed record GameForkHealthProbe(
    string Type,
    string Path,
    string? ReadyField = null,
    int? ReadyHttpStatus = 200)
{
    public static GameForkHealthProbe File(string path, string readyField = "sessionStarted") =>
        new("file", path, ReadyField: readyField);

    public static GameForkHealthProbe Http(string path, int readyStatus = 200) =>
        new("http", path, ReadyHttpStatus: readyStatus);

    public static GameForkHealthProbe None { get; } = new("none", "");
}

/// <summary>Inputs for <see cref="IGameForkAdapter.ProbeHealthAsync"/>.</summary>
public sealed record GameForkHealthContext(
    string? StatusFileContents = null,
    int? HttpStatusCode = null,
    string? HttpBody = null);

/// <summary>Result of a fork readiness probe.</summary>
public sealed record GameForkHealthResult(bool Ready, string Detail);

/// <summary>Default health probe helpers shared by manifest-backed adapters.</summary>
public static class GameForkHealth
{
    public static GameForkHealthResult Evaluate(GameForkHealthProbe probe, GameForkHealthContext context)
    {
        if (string.Equals(probe.Type, "none", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(probe.Path))
        {
            return new GameForkHealthResult(true, "no health probe configured");
        }

        if (string.Equals(probe.Type, "file", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(context.StatusFileContents))
            {
                return new GameForkHealthResult(false, "status file missing or empty");
            }

            var field = probe.ReadyField ?? "sessionStarted";
            if (context.StatusFileContents.Contains($"\"{field}\":true", StringComparison.OrdinalIgnoreCase)
                || context.StatusFileContents.Contains($"\"{field}\": true", StringComparison.OrdinalIgnoreCase))
            {
                return new GameForkHealthResult(true, $"file probe ready ({field}=true)");
            }

            return new GameForkHealthResult(false, $"file probe not ready (expected {field}=true)");
        }

        if (string.Equals(probe.Type, "http", StringComparison.OrdinalIgnoreCase))
        {
            var expected = probe.ReadyHttpStatus ?? 200;
            if (context.HttpStatusCode == expected)
            {
                return new GameForkHealthResult(true, $"http {expected} OK");
            }

            return new GameForkHealthResult(
                false,
                $"http status {context.HttpStatusCode?.ToString() ?? "null"} (expected {expected})");
        }

        return new GameForkHealthResult(false, $"unknown health probe type '{probe.Type}'");
    }
}
