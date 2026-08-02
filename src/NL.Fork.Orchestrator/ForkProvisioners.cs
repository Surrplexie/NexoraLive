using System.Diagnostics;
using System.Text.Json;
using NL.Fork.Core;
using NL.Fork.Orchestrator.Core;

namespace NL.Fork.Orchestrator;

public sealed class MockForkProvisioner : IForkProvisioner
{
    public ForkProvisionerKind Kind => ForkProvisionerKind.Mock;

    public Task<ForkProvisionerStartResult> StartAsync(
        ForkProvisionerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var status = new
        {
            connected = true,
            provisioner = "mock",
            sessionId = request.SessionId,
            bridgeUrl = request.BridgeWebSocketUrl,
            checkedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(
            Path.Combine(request.WorkspacePath, "fork-status.json"),
            JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));

        return Task.FromResult(new ForkProvisionerStartResult(
            true,
            ContainerOrProcessId: $"mock-{request.SessionId}",
            ForkConnectEndpoint: $"mock://fork/{request.SessionId}"));
    }

    public Task StopAsync(ForkOrchestratorSession session, CancellationToken cancellationToken = default)
    {
        var statusPath = Path.Combine(session.WorkspacePath, "fork-status.json");
        if (File.Exists(statusPath))
        {
            File.Delete(statusPath);
        }

        return Task.CompletedTask;
    }
}

public sealed class ProcessForkProvisioner : IForkProvisioner
{
    private readonly string? _runtimeProjectPath;
    private readonly Action<string>? _log;

    public ProcessForkProvisioner(string? runtimeProjectPath = null, Action<string>? log = null)
    {
        _runtimeProjectPath = runtimeProjectPath;
        _log = log;
    }

    public ForkProvisionerKind Kind => ForkProvisionerKind.Process;

    public async Task<ForkProvisionerStartResult> StartAsync(
        ForkProvisionerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var runtimeDll = ResolveRuntimeDll();
        if (runtimeDll is null)
        {
            return new ForkProvisionerStartResult(false, Error: "NL.Fork.Runtime not found.");
        }

        var statusPath = Path.Combine(request.WorkspacePath, "fork-status.json");
        var profile = ForkGameProfiles.Resolve(request.GameId);
        var gameArg = profile.GameArg;
        var args =
            $"\"{runtimeDll}\" --game {gameArg} --url \"{request.BridgeWebSocketUrl}\" " +
            $"--mods \"{request.ModsJsonPath}\" --status \"{statusPath}\" " +
            $"--admit-url \"{request.AdmitUrl}\" --loop --interval 8";

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(runtimeDll) ?? request.WorkspacePath,
        };

        var process = Process.Start(psi);
        if (process is null)
        {
            return new ForkProvisionerStartResult(false, Error: "Failed to start fork process.");
        }

        _log?.Invoke($"[orchestrator] started process fork pid={process.Id} session={request.SessionId}");
        await Task.Delay(500, cancellationToken);

        return new ForkProvisionerStartResult(
            true,
            ContainerOrProcessId: process.Id.ToString(),
            ForkConnectEndpoint: $"process://localhost/{process.Id}");
    }

    public Task StopAsync(ForkOrchestratorSession session, CancellationToken cancellationToken = default)
    {
        if (int.TryParse(session.ContainerOrProcessId, out var pid))
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // already exited
            }
        }

        return Task.CompletedTask;
    }

    private string? ResolveRuntimeDll()
    {
        if (!string.IsNullOrWhiteSpace(_runtimeProjectPath))
        {
            var built = Path.Combine(_runtimeProjectPath, "bin", "Release", "net8.0", "NL.Fork.Runtime.dll");
            if (File.Exists(built))
            {
                return built;
            }
        }

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "src", "NL.Fork.Runtime", "bin", "Release", "net8.0", "NL.Fork.Runtime.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName ?? "";
        }

        return null;
    }
}

public sealed class DockerForkProvisioner : IForkProvisioner
{
    private readonly Action<string>? _log;

    public DockerForkProvisioner(Action<string>? log = null) => _log = log;

    public ForkProvisionerKind Kind => ForkProvisionerKind.Docker;

    public async Task<ForkProvisionerStartResult> StartAsync(
        ForkProvisionerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await DockerAvailableAsync(cancellationToken))
        {
            return new ForkProvisionerStartResult(false, Error: "Docker CLI not available.");
        }

        var image = string.IsNullOrWhiteSpace(request.DockerImage) ? "nl-fork-hello:latest" : request.DockerImage;
        var name = $"nl-fork-{request.SessionId}".ToLowerInvariant();
        var ws = request.WorkspacePath.Replace('\\', '/');
        var profile = ForkGameProfiles.Resolve(request.GameId);
        var gameArg = profile.GameArg;
        var portMap = profile.PlayerConnectPort is int port
            ? $"-p {port}:{port} "
            : "";

        var args =
            $"run -d --rm --name {name} " +
            portMap +
            $"-v \"{ws}:/data\" " +
            $"-e NL_FORK_WS_URL={request.BridgeWebSocketUrl} " +
            $"-e NL_FORK_MODS=/data/mods.json " +
            $"-e NL_FORK_STATUS=/data/fork-status.json " +
            $"-e NL_FORK_ADMIT_URL={request.AdmitUrl} " +
            $"-e NL_FORK_GAME={gameArg} " +
            $"-e NL_DATA_ROOT=/data " +
            $"{image} --game {gameArg} --loop --interval 8";

        var (code, output, err) = await RunDockerAsync(args, cancellationToken);
        if (code != 0)
        {
            return new ForkProvisionerStartResult(false, Error: err.Trim().Length > 0 ? err : output);
        }

        var containerId = output.Trim();
        _log?.Invoke($"[orchestrator] docker container {name} id={containerId}");
        var connect = BuildConnectEndpoint(profile, name, port: profile.PlayerConnectPort);
        return new ForkProvisionerStartResult(
            true,
            ContainerOrProcessId: containerId,
            ForkConnectEndpoint: connect);
    }

    private static string BuildConnectEndpoint(ForkGameProfile profile, string containerName, int? port)
    {
        if (port is int p && string.Equals(profile.ConnectScheme, "minecraft", StringComparison.OrdinalIgnoreCase))
        {
            return $"minecraft://127.0.0.1:{p}";
        }

        if (string.Equals(profile.ConnectScheme, "beamng-sidecar", StringComparison.OrdinalIgnoreCase))
        {
            return $"beamng-sidecar://127.0.0.1/udp/27022";
        }

        return $"docker://{containerName}";
    }

    public async Task StopAsync(ForkOrchestratorSession session, CancellationToken cancellationToken = default)
    {
        var name = $"nl-fork-{session.SessionId}".ToLowerInvariant();
        await RunDockerAsync($"rm -f {name}", cancellationToken);
    }

    private static async Task<bool> DockerAvailableAsync(CancellationToken cancellationToken)
    {
        var (code, _, _) = await RunDockerAsync("version", cancellationToken);
        return code == 0;
    }

    private static async Task<(int Code, string Output, string Error)> RunDockerAsync(
        string args,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return (-1, "", "docker start failed");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await outputTask, await errTask);
    }
}
