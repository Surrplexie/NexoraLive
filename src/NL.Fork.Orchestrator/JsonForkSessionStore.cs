using System.Text.Json;
using NL.Fork.Orchestrator.Core;

namespace NL.Fork.Orchestrator;

public sealed class JsonForkSessionStore : IForkSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, ForkOrchestratorSession> _sessions = new();

    public JsonForkSessionStore(string? path = null)
    {
        _path = path ?? NlForkOrchestratorPaths.Store;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Load();
    }

    public ForkOrchestratorSession? Get(string sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var s) ? s : null;
        }
    }

    public ForkOrchestratorSession? GetActiveForStreamer(string streamerId)
    {
        lock (_lock)
        {
            return _sessions.Values.FirstOrDefault(s =>
                string.Equals(s.StreamerId, streamerId, StringComparison.OrdinalIgnoreCase)
                && s.State is ForkSessionState.Pending or ForkSessionState.Running or ForkSessionState.Stopping);
        }
    }

    public IReadOnlyList<ForkOrchestratorSession> ListActive()
    {
        lock (_lock)
        {
            return _sessions.Values
                .Where(s => s.State is ForkSessionState.Pending or ForkSessionState.Running or ForkSessionState.Stopping)
                .OrderByDescending(s => s.CreatedAtUtc)
                .ToList();
        }
    }

    public void Save(ForkOrchestratorSession session)
    {
        lock (_lock)
        {
            _sessions[session.SessionId] = session;
            File.WriteAllText(_path, JsonSerializer.Serialize(_sessions.Values.ToList(), JsonOptions));
        }
    }

    public void Remove(string sessionId)
    {
        lock (_lock)
        {
            _sessions.Remove(sessionId);
            File.WriteAllText(_path, JsonSerializer.Serialize(_sessions.Values.ToList(), JsonOptions));
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        var json = File.ReadAllText(_path);
        var list = JsonSerializer.Deserialize<List<ForkOrchestratorSession>>(json, JsonOptions);
        if (list is null)
        {
            return;
        }

        _sessions = list.ToDictionary(s => s.SessionId, StringComparer.OrdinalIgnoreCase);
    }
}
