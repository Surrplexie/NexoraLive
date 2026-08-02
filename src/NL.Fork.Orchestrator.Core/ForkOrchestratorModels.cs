namespace NL.Fork.Orchestrator.Core;

public enum ForkProvisionerKind
{
    Mock,
    Process,
    Docker,
}

public enum ForkSessionState
{
    Pending,
    Running,
    Stopping,
    Destroyed,
    Failed,
}

public sealed record CreateForkSessionRequest(
    string StreamerId,
    string GameId,
    string MajorVersion,
    string NlePath,
    IReadOnlyList<string> ModIds,
    string? DockerImage = null,
    string? ImageDigest = null,
    int ReservedPrivilegedSlots = 2);

public sealed record ForkOrchestratorSession(
    string SessionId,
    string StreamerId,
    string GameId,
    string MajorVersion,
    ForkSessionState State,
    ForkProvisionerKind Provisioner,
    string WorkspacePath,
    string BridgeConnectUrl,
    string AdmitUrl,
    string ForkConnectEndpoint,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DestroyAfterUtc = null,
    DateTimeOffset? GraceDestroyAtUtc = null,
    string? ContainerOrProcessId = null,
    string? BusToken = null,
    int ReservedPrivilegedSlots = 2,
    DateTimeOffset? IdleSinceUtc = null,
    int StreamerQuotaUnits = 1,
    string? LastError = null);

public sealed record ForkProvisionerStartRequest(
    string SessionId,
    string WorkspacePath,
    string BridgeWebSocketUrl,
    string AdmitUrl,
    string ModsJsonPath,
    string NlePath,
    string? DockerImage = null,
    string? GameId = null);

public sealed record ForkProvisionerStartResult(
    bool Success,
    string? ContainerOrProcessId = null,
    string? ForkConnectEndpoint = null,
    string? Error = null);

public sealed record ForkOrchestratorCreateResult(
    bool Success,
    ForkOrchestratorSession? Session = null,
    string? Error = null);

public sealed record ForkOrchestratorDestroyResult(
    bool Success,
    string? Error = null);

public sealed record ForkSessionWorkspaceLayout(
    string Root,
    string RulesNlePath,
    string ModsJsonPath,
    string WorldPath,
    string ForkStatusPath,
    string SessionMetaPath);
