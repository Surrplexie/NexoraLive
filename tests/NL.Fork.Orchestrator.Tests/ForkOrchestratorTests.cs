using NL.Fork.Orchestrator;
using NL.Fork.Orchestrator.Core;
using NL.Server;
using NL.Server.Core.Integration;
using Xunit;

namespace NL.Fork.Orchestrator.Tests;

public class ForkOrchestratorTests
{
    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nl-orch-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("NL_FORK_ORCHESTRATOR_ROOT", dir);
        return dir;
    }

    private static string TempNle()
    {
        var path = Path.Combine(Path.GetTempPath(), "nl-orch-nle-" + Guid.NewGuid().ToString("N") + ".nle");
        File.WriteAllText(path, "# test rules\n");
        return path;
    }

    [Fact]
    public async Task MockProvisioner_CreateAndDestroy_WipesWorkspace()
    {
        var root = TempRoot();
        var nle = TempNle();
        var settings = new NlForkOrchestratorSettings { Enabled = true, Mode = NlForkProvisionerMode.Mock };
        var store = new JsonForkSessionStore(Path.Combine(root, "store.json"));
        var audit = new JsonlForkOrchestratorAuditStore(Path.Combine(root, "audit.jsonl"));
        var provisioners = new Dictionary<ForkProvisionerKind, IForkProvisioner>
        {
            [ForkProvisionerKind.Mock] = new MockForkProvisioner(),
        };
        var svc = new NlForkOrchestratorService(settings, store, audit, provisioners);

        var create = await svc.CreateSessionAsync(
            new CreateForkSessionRequest("streamer1", "gameA", "1.0", nle, []),
            "ws://127.0.0.1:27021/nl/v1?token=test",
            "http://127.0.0.1:27020/api/v1/session/admit",
            "test-token");

        Assert.True(create.Success);
        Assert.NotNull(create.Session);
        Assert.Equal(ForkSessionState.Running, create.Session!.State);
        Assert.True(File.Exists(Path.Combine(create.Session.WorkspacePath, "fork-status.json")));

        var destroy = await svc.DestroySessionAsync(create.Session.SessionId);
        Assert.True(destroy.Success);
        Assert.Null(svc.GetSession(create.Session.SessionId));
        Assert.False(Directory.Exists(create.Session.WorkspacePath));
    }

    [Fact]
    public async Task CreateSession_RejectsDuplicateActiveStreamer()
    {
        var root = TempRoot();
        var nle = TempNle();
        var settings = new NlForkOrchestratorSettings { Enabled = true, Mode = NlForkProvisionerMode.Mock };
        var store = new JsonForkSessionStore(Path.Combine(root, "store.json"));
        var audit = new JsonlForkOrchestratorAuditStore(Path.Combine(root, "audit.jsonl"));
        var provisioners = new Dictionary<ForkProvisionerKind, IForkProvisioner>
        {
            [ForkProvisionerKind.Mock] = new MockForkProvisioner(),
        };
        var svc = new NlForkOrchestratorService(settings, store, audit, provisioners);

        var first = await svc.CreateSessionAsync(
            new CreateForkSessionRequest("dup", "gameA", "1.0", nle, []),
            "ws://127.0.0.1/nl/v1",
            "http://127.0.0.1/admit",
            "tok");
        Assert.True(first.Success);

        var second = await svc.CreateSessionAsync(
            new CreateForkSessionRequest("dup", "gameA", "1.0", nle, []),
            "ws://127.0.0.1/nl/v1",
            "http://127.0.0.1/admit",
            "tok");
        Assert.False(second.Success);
        Assert.Contains("already has active", second.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Manifest_IncludesForkConnectFields()
    {
        var bus = new NL.Server.Core.Integration.NlSessionBusInfo
        {
            SessionId = "sess1",
            Token = "secret",
            WebSocketUrl = "ws://127.0.0.1:27021/nl/v1",
            HttpBaseUrl = "http://127.0.0.1:27020",
            WebSocketPort = 27021,
            HttpPort = 27020,
        };
        var profile = new SessionProfileFile
        {
            StreamerId = "s1",
            ForkOrchestratorEnabled = true,
            ConfigPath = "rules.nle",
        };
        var fork = new ForkManifestConnectInfo("fork12", "mock://fork/fork12", "Mock", 2);

        var manifest = NlSessionServerHelper.CreateManifest(
            bus, profile, "127.0.0.1", 27020, 27021, 27030, true, fork);

        Assert.Equal("fork12", manifest.ForkSessionId);
        Assert.Equal("mock://fork/fork12", manifest.ForkConnectEndpoint);
        Assert.Equal("Mock", manifest.ForkProvisioner);
        Assert.True(manifest.ForkOrchestratorEnabled);
        Assert.Equal(2, manifest.ReservedPrivilegedSlots);
    }

    [Fact]
    public async Task GraceDestroy_SchedulesThenDestroysOnTick()
    {
        var root = TempRoot();
        var nle = TempNle();
        var settings = new NlForkOrchestratorSettings
        {
            Enabled = true,
            Mode = NlForkProvisionerMode.Mock,
            DestroyGraceSeconds = 0,
        };
        var store = new JsonForkSessionStore(Path.Combine(root, "store.json"));
        var audit = new JsonlForkOrchestratorAuditStore(Path.Combine(root, "audit.jsonl"));
        var provisioners = new Dictionary<ForkProvisionerKind, IForkProvisioner>
        {
            [ForkProvisionerKind.Mock] = new MockForkProvisioner(),
        };
        var svc = new NlForkOrchestratorService(settings, store, audit, provisioners);

        var create = await svc.CreateSessionAsync(
            new CreateForkSessionRequest("grace", "gameA", "1.0", nle, []),
            "ws://127.0.0.1/nl/v1",
            "http://127.0.0.1/admit",
            "tok");
        Assert.True(create.Success);

        var schedule = await svc.ScheduleGraceDestroyForStreamerAsync("grace");
        Assert.True(schedule.Success);
        Assert.Null(svc.GetActiveForStreamer("grace"));
    }

    [Fact]
    public async Task GraceDestroy_DoesNotRescheduleWhenAlreadyStopping()
    {
        var root = TempRoot();
        var nle = TempNle();
        var settings = new NlForkOrchestratorSettings
        {
            Enabled = true,
            Mode = NlForkProvisionerMode.Mock,
            DestroyGraceSeconds = 30,
        };
        var store = new JsonForkSessionStore(Path.Combine(root, "store.json"));
        var audit = new JsonlForkOrchestratorAuditStore(Path.Combine(root, "audit.jsonl"));
        var provisioners = new Dictionary<ForkProvisionerKind, IForkProvisioner>
        {
            [ForkProvisionerKind.Mock] = new MockForkProvisioner(),
        };
        var svc = new NlForkOrchestratorService(settings, store, audit, provisioners);

        var create = await svc.CreateSessionAsync(
            new CreateForkSessionRequest("grace", "gameA", "1.0", nle, []),
            "ws://127.0.0.1/nl/v1",
            "http://127.0.0.1/admit",
            "tok");
        Assert.True(create.Success);

        var first = await svc.ScheduleGraceDestroyForStreamerAsync("grace");
        Assert.True(first.Success);
        var sessionId = create.Session!.SessionId;
        var session = svc.GetSession(sessionId);
        Assert.NotNull(session);
        var firstGrace = session!.GraceDestroyAtUtc;
        Assert.NotNull(firstGrace);

        await Task.Delay(50);
        var second = await svc.ScheduleGraceDestroyForStreamerAsync("grace");
        Assert.True(second.Success);
        session = svc.GetSession(sessionId);
        Assert.Equal(firstGrace, session!.GraceDestroyAtUtc);
    }

    [Fact]
    public void DockerProvisioner_RewritesLocalHostForContainerBridge()
    {
        var ws = DockerForkProvisioner.RewriteLocalHostForDocker(
            "ws://127.0.0.1:27021/nl/v1?token=abc",
            "host.docker.internal");
        Assert.Equal("ws://host.docker.internal:27021/nl/v1?token=abc", ws);

        var admit = DockerForkProvisioner.RewriteLocalHostForDocker(
            "http://localhost:27020/api/v1/session/admit",
            "host.docker.internal");
        Assert.Equal("http://host.docker.internal:27020/api/v1/session/admit", admit);
    }

    [Fact]
    public void DockerProvisioner_RewritesWorkspacePathForHostBindMount()
    {
        Environment.SetEnvironmentVariable("NL_DATA_ROOT", "/data");
        Environment.SetEnvironmentVariable("NL_FORK_DOCKER_WORKSPACE_HOST_ROOT", "C:/nl/production-data");
        try
        {
            var hostPath = DockerForkProvisioner.ResolveWorkspaceMountPath(
                "/data/fork-orchestrator/abc123");
            Assert.Equal("C:/nl/production-data/fork-orchestrator/abc123", hostPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NL_DATA_ROOT", null);
            Environment.SetEnvironmentVariable("NL_FORK_DOCKER_WORKSPACE_HOST_ROOT", null);
        }
    }
}
