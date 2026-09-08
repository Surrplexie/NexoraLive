using NL.Fork.Catalog;
using NL.Fork.Orchestrator.Core;

namespace NL.Fork.Orchestrator;

public enum NlForkProvisionerMode
{
    Mock,
    Process,
    Docker,
    Kubernetes,
    Auto,
}

public sealed class NlForkOrchestratorSettings
{
    public const string EnabledVariable = "NL_FORK_ORCHESTRATOR_ENABLED";
    public const string ModeVariable = "NL_FORK_ORCHESTRATOR_MODE";

    public bool Enabled { get; init; }

    public NlForkProvisionerMode Mode { get; init; } = NlForkProvisionerMode.Auto;

    public int DestroyGraceSeconds { get; init; } = 30;

    public double MaxSessionHours { get; init; } = 12;

    public int DefaultReservedPrivilegedSlots { get; init; } = 2;

    public string DefaultDockerImage { get; init; } = "nl-fork-hello:latest";

    public string KubernetesNamespace { get; init; } = "nl-fork";

    public string? KubernetesKubeconfig { get; init; }

    public int IdleDetectionMinutes { get; init; } = 15;

    public int StreamerQuotaPlaceholder { get; init; } = 1;

    public static NlForkOrchestratorSettings LoadFromEnvironment()
    {
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable(EnabledVariable),
            "1",
            StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable(EnabledVariable),
                "true",
                StringComparison.OrdinalIgnoreCase);

        var modeRaw = Environment.GetEnvironmentVariable(ModeVariable)?.Trim().ToLowerInvariant();
        var mode = modeRaw switch
        {
            "mock" => NlForkProvisionerMode.Mock,
            "process" => NlForkProvisionerMode.Process,
            "docker" => NlForkProvisionerMode.Docker,
            "kubernetes" or "k8s" => NlForkProvisionerMode.Kubernetes,
            _ => NlForkProvisionerMode.Auto,
        };

        var grace = int.TryParse(Environment.GetEnvironmentVariable("NL_FORK_DESTROY_GRACE_SEC"), out var g)
            ? Math.Max(0, g)
            : 30;

        var maxHours = double.TryParse(Environment.GetEnvironmentVariable("NL_FORK_SESSION_MAX_HOURS"), out var h)
            ? Math.Max(0.25, h)
            : 12;

        var reserved = int.TryParse(Environment.GetEnvironmentVariable("NL_FORK_RESERVED_PRIVILEGED_SLOTS"), out var r)
            ? Math.Max(0, r)
            : 2;

        var dockerImage = Environment.GetEnvironmentVariable("NL_FORK_DOCKER_IMAGE") ?? "nl-fork-hello:latest";

        var idleMinutes = int.TryParse(Environment.GetEnvironmentVariable("NL_FORK_IDLE_MINUTES"), out var idle)
            ? Math.Max(0, idle)
            : 15;

        var k8sNs = Environment.GetEnvironmentVariable("NL_FORK_K8S_NAMESPACE") ?? "nl-fork";

        return new NlForkOrchestratorSettings
        {
            Enabled = enabled,
            Mode = mode,
            DestroyGraceSeconds = grace,
            MaxSessionHours = maxHours,
            DefaultReservedPrivilegedSlots = reserved,
            DefaultDockerImage = dockerImage,
            KubernetesNamespace = k8sNs,
            KubernetesKubeconfig = Environment.GetEnvironmentVariable("NL_FORK_K8S_KUBECONFIG"),
            IdleDetectionMinutes = idleMinutes,
        };
    }

    public object ToPublicInfo() => new
    {
        enabled = Enabled,
        mode = Mode.ToString(),
        destroyGraceSeconds = DestroyGraceSeconds,
        maxSessionHours = MaxSessionHours,
        reservedPrivilegedSlots = DefaultReservedPrivilegedSlots,
        dockerImage = DefaultDockerImage,
        kubernetesNamespace = KubernetesNamespace,
        storePath = NlForkOrchestratorPaths.Root,
    };
}

public sealed class NlForkOrchestratorHost
{
    public NlForkOrchestratorHost(
        NlForkOrchestratorSettings settings,
        NlForkCatalogHost? catalog = null,
        string? runtimeProjectPath = null,
        Action<string>? log = null)
    {
        Settings = settings;
        Catalog = catalog;
        Store = new JsonForkSessionStore();
        Audit = new JsonlForkOrchestratorAuditStore();
        Provisioners = BuildProvisioners(settings, runtimeProjectPath, log);
        Orchestrator = new NlForkOrchestratorService(settings, Store, Audit, Provisioners, catalog);
    }

    public NlForkOrchestratorSettings Settings { get; }

    public NlForkCatalogHost? Catalog { get; }

    public IForkSessionStore Store { get; }

    public JsonlForkOrchestratorAuditStore Audit { get; }

    public IReadOnlyDictionary<ForkProvisionerKind, IForkProvisioner> Provisioners { get; }

    public NlForkOrchestratorService Orchestrator { get; }

    private static IReadOnlyDictionary<ForkProvisionerKind, IForkProvisioner> BuildProvisioners(
        NlForkOrchestratorSettings settings,
        string? runtimeProjectPath,
        Action<string>? log)
    {
        var map = new Dictionary<ForkProvisionerKind, IForkProvisioner>
        {
            [ForkProvisionerKind.Mock] = new MockForkProvisioner(),
            [ForkProvisionerKind.Process] = new ProcessForkProvisioner(runtimeProjectPath, log),
            [ForkProvisionerKind.Docker] = new DockerForkProvisioner(log),
            [ForkProvisionerKind.Kubernetes] = new KubernetesForkProvisioner(settings, log),
        };

        return map;
    }

    public ForkProvisionerKind ResolveProvisionerKind() => Orchestrator.ResolveProvisionerKind();
}
