using NL.Fork.Orchestrator.Core;

namespace NL.Fork.Orchestrator;

public sealed class JsonlForkOrchestratorAuditStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public JsonlForkOrchestratorAuditStore(string? path = null) =>
        _path = path ?? NlForkOrchestratorPaths.Audit;

    public void Append(string action, ForkOrchestratorSession session, string? detail = null)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var line = System.Text.Json.JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            action,
            sessionId = session.SessionId,
            streamerId = session.StreamerId,
            gameId = session.GameId,
            majorVersion = session.MajorVersion,
            state = session.State.ToString(),
            provisioner = session.Provisioner.ToString(),
            detail,
        });

        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
