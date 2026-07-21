namespace NL.Server;

/// <summary>Cross-platform session host state (WinForms + web dashboard).</summary>
public enum SessionHostState
{
    Idle,
    Running,
    Stopping,
}

/// <summary>
/// Runs one <see cref="NlSessionRunner"/> session with a thread-safe log buffer.
/// Shared by Windows Session Host and <c>NL.SessionHost.Web</c>.
/// </summary>
public sealed class SessionHostService
{
    private readonly object _lock = new();
    private readonly List<string> _logLines = new();
    private const int MaxLogLines = 2000;

    private CancellationTokenSource? _cts;
    private Task<int>? _runTask;
    private NlSessionRunner? _runner;

    public SessionHostState State { get; private set; } = SessionHostState.Idle;

    public int DecisionCount => _runner?.Host?.Decisions.Count ?? 0;

    public IReadOnlyList<string> GetLogSnapshot()
    {
        lock (_lock)
        {
            return _logLines.ToArray();
        }
    }

    public event Action<string>? LogAppended;

    public event Action? StateChanged;

    public async Task<int> StartAsync(NlSessionOptions options, CancellationToken externalCancellation = default)
    {
        lock (_lock)
        {
            if (State == SessionHostState.Running)
            {
                throw new InvalidOperationException("Session already running.");
            }

            State = SessionHostState.Running;
            _logLines.Clear();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
            _runner = new NlSessionRunner
            {
                Options = options,
                Log = AppendLog,
            };
            _runTask = _runner.RunAsync(_cts.Token);
        }

        NotifyStateChanged();
        AppendLog("Session started.");

        try
        {
            return await _runTask.ConfigureAwait(false);
        }
        finally
        {
            lock (_lock)
            {
                State = SessionHostState.Idle;
                _cts?.Dispose();
                _cts = null;
                _runTask = null;
            }

            NotifyStateChanged();
            AppendLog("Session ended.");
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (State != SessionHostState.Running)
            {
                return;
            }

            State = SessionHostState.Stopping;
            _cts?.Cancel();
        }

        NotifyStateChanged();
        AppendLog("Stop requested…");
    }

    public bool IsRunning => State == SessionHostState.Running;

    private void AppendLog(string line)
    {
        lock (_lock)
        {
            _logLines.Add(line);
            if (_logLines.Count > MaxLogLines)
            {
                _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
            }
        }

        LogAppended?.Invoke(line);
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
