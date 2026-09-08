namespace NL.Fork.Orchestrator.Core;

public interface IForkProvisioner
{
    ForkProvisionerKind Kind { get; }

    Task<ForkProvisionerStartResult> StartAsync(
        ForkProvisionerStartRequest request,
        CancellationToken cancellationToken = default);

    Task StopAsync(
        ForkOrchestratorSession session,
        CancellationToken cancellationToken = default);
}

public interface IForkSessionStore
{
    ForkOrchestratorSession? Get(string sessionId);

    ForkOrchestratorSession? GetActiveForStreamer(string streamerId);

    IReadOnlyList<ForkOrchestratorSession> ListActive();

    void Save(ForkOrchestratorSession session);

    void Remove(string sessionId);
}
