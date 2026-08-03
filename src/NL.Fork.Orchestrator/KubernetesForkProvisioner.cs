using System.Diagnostics;
using System.Text;
using NL.Fork.Core;
using NL.Fork.Orchestrator.Core;

namespace NL.Fork.Orchestrator;

/// <summary>Phase S — Kubernetes Job provisioner for multi-node staging/production fleets.</summary>
public static class KubernetesForkJobManifestBuilder
{
    public static string BuildConfigMapName(string sessionId) => $"nl-fork-cm-{sessionId}".ToLowerInvariant();

    public static string BuildJobName(string sessionId) => $"nl-fork-{sessionId}".ToLowerInvariant();

    public static string BuildConfigMapYaml(
        string sessionId,
        string namespaceName,
        string nleContent,
        string modsJsonContent)
    {
        var cmName = BuildConfigMapName(sessionId);
        return $"""
            apiVersion: v1
            kind: ConfigMap
            metadata:
              name: {cmName}
              namespace: {namespaceName}
              labels:
                app: nl-fork
                nl.session: "{sessionId}"
            data:
              rules.nle: |
            {IndentYamlLiteral(nleContent)}
              mods.json: |
            {IndentYamlLiteral(modsJsonContent)}
            """;
    }

    public static string BuildJobYaml(
        ForkProvisionerStartRequest request,
        string image,
        string namespaceName,
        string gameArg)
    {
        var jobName = BuildJobName(request.SessionId);
        var cmName = BuildConfigMapName(request.SessionId);
        return $"""
            apiVersion: batch/v1
            kind: Job
            metadata:
              name: {jobName}
              namespace: {namespaceName}
              labels:
                app: nl-fork
                nl.session: "{request.SessionId}"
            spec:
              ttlSecondsAfterFinished: 300
              backoffLimit: 0
              template:
                metadata:
                  labels:
                    app: nl-fork
                    nl.session: "{request.SessionId}"
                spec:
                  restartPolicy: Never
                  containers:
                    - name: fork
                      image: {image}
                      args: ["--game", "{gameArg}", "--loop", "--interval", "8"]
                      env:
                        - name: NL_FORK_WS_URL
                          value: "{EscapeYaml(request.BridgeWebSocketUrl)}"
                        - name: NL_FORK_MODS
                          value: "/data/mods.json"
                        - name: NL_FORK_STATUS
                          value: "/data/fork-status.json"
                        - name: NL_FORK_ADMIT_URL
                          value: "{EscapeYaml(request.AdmitUrl)}"
                        - name: NL_FORK_GAME
                          value: "{gameArg}"
                        - name: NL_DATA_ROOT
                          value: "/data"
                      volumeMounts:
                        - name: fork-data
                          mountPath: /data
                  volumes:
                    - name: fork-data
                      configMap:
                        name: {cmName}
            """;
    }

    private static string IndentYamlLiteral(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "    ";
        }

        return string.Join("\n", content.Split('\n').Select(l => "    " + l));
    }

    private static string EscapeYaml(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public sealed class KubernetesForkProvisioner : IForkProvisioner
{
    private readonly NlForkOrchestratorSettings _settings;
    private readonly Action<string>? _log;

    public KubernetesForkProvisioner(NlForkOrchestratorSettings settings, Action<string>? log = null)
    {
        _settings = settings;
        _log = log;
    }

    public ForkProvisionerKind Kind => ForkProvisionerKind.Kubernetes;

    public async Task<ForkProvisionerStartResult> StartAsync(
        ForkProvisionerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await KubectlAvailableAsync(cancellationToken))
        {
            return new ForkProvisionerStartResult(false, Error: "kubectl not available.");
        }

        var ns = _settings.KubernetesNamespace;
        var image = string.IsNullOrWhiteSpace(request.DockerImage)
            ? _settings.DefaultDockerImage
            : request.DockerImage;
        var profile = ForkGameProfiles.Resolve(request.GameId);
        var gameArg = profile.GameArg;

        var nleContent = File.Exists(request.NlePath)
            ? await File.ReadAllTextAsync(request.NlePath, cancellationToken)
            : "# empty\n";
        var modsContent = File.Exists(request.ModsJsonPath)
            ? await File.ReadAllTextAsync(request.ModsJsonPath, cancellationToken)
            : "{}";

        var cmYaml = KubernetesForkJobManifestBuilder.BuildConfigMapYaml(
            request.SessionId, ns, nleContent, modsContent);
        var jobYaml = KubernetesForkJobManifestBuilder.BuildJobYaml(request, image, ns, gameArg);

        var (nsCode, _, nsErr) = await RunKubectlAsync($"create namespace {ns}", cancellationToken);
        if (nsCode != 0 && !nsErr.Contains("AlreadyExists", StringComparison.OrdinalIgnoreCase))
        {
            await RunKubectlAsync($"get namespace {ns}", cancellationToken);
        }

        var (cmCode, _, cmErr) = await RunKubectlAsync($"apply -f -", cancellationToken, cmYaml);
        if (cmCode != 0)
        {
            return new ForkProvisionerStartResult(false, Error: $"ConfigMap apply failed: {cmErr}");
        }

        var (jobCode, jobOut, jobErr) = await RunKubectlAsync($"apply -f -", cancellationToken, jobYaml);
        if (jobCode != 0)
        {
            return new ForkProvisionerStartResult(false, Error: $"Job apply failed: {jobErr}");
        }

        var jobName = KubernetesForkJobManifestBuilder.BuildJobName(request.SessionId);
        _log?.Invoke($"[orchestrator] k8s job {jobName} ns={ns}");
        var connect = $"k8s://{ns}/job/{jobName}";
        return new ForkProvisionerStartResult(true, ContainerOrProcessId: jobName, ForkConnectEndpoint: connect);
    }

    public async Task StopAsync(ForkOrchestratorSession session, CancellationToken cancellationToken = default)
    {
        var ns = _settings.KubernetesNamespace;
        var jobName = KubernetesForkJobManifestBuilder.BuildJobName(session.SessionId);
        var cmName = KubernetesForkJobManifestBuilder.BuildConfigMapName(session.SessionId);
        await RunKubectlAsync($"delete job {jobName} -n {ns} --ignore-not-found", cancellationToken);
        await RunKubectlAsync($"delete configmap {cmName} -n {ns} --ignore-not-found", cancellationToken);
    }

    private async Task<bool> KubectlAvailableAsync(CancellationToken cancellationToken)
    {
        var (code, _, _) = await RunKubectlAsync("version --client", cancellationToken);
        return code == 0;
    }

    private async Task<(int Code, string Output, string Error)> RunKubectlAsync(
        string args,
        CancellationToken cancellationToken,
        string? stdin = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "kubectl",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(_settings.KubernetesKubeconfig))
        {
            psi.Environment["KUBECONFIG"] = _settings.KubernetesKubeconfig!;
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return (-1, "", "kubectl start failed");
        }

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await outputTask, await errTask);
    }
}
