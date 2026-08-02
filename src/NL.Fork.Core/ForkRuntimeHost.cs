using System.Text.Json;
using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>Runs a game fork runtime against a remote NL session bus with optional demo loop.</summary>
public sealed class ForkRuntimeHost : IAsyncDisposable
{
    private readonly IForkRuntimeDetails _runtime;
    private readonly ForkNlBridgeClient? _bridge;
    private readonly ForkGameKind _game;
    private readonly string? _statusPath;
    private readonly Action<string>? _log;

    public ForkRuntimeHost(
        IForkRuntimeDetails runtime,
        ForkGameKind game,
        ForkNlBridgeClient? bridge,
        string? statusPath = null,
        Action<string>? log = null)
    {
        _runtime = runtime;
        _game = game;
        _bridge = bridge;
        _statusPath = statusPath;
        _log = log;
    }

    public IForkRuntimeDetails Runtime => _runtime;

    public ForkGameKind Game => _game;

    public static ForkRuntimeHost CreateEmbedded(
        ForkGameKind game,
        string nleSource,
        ForkModManifest? mods = null,
        IJoinGate? joinGate = null)
    {
        var runtime = ForkRuntimeFactory.CreateEmbedded(game, nleSource, mods, joinGate);
        return new ForkRuntimeHost(runtime, game, bridge: null);
    }

    public static ForkRuntimeHost CreateRemote(
        ForkGameKind game,
        ForkNlBridgeClient bridge,
        ForkModManifest? mods = null,
        Func<string, Task<bool>>? admitAsync = null,
        string? statusPath = null,
        Action<string>? log = null)
    {
        var runtime = ForkRuntimeFactory.Create(game, bridge, mods, admitAsync);
        return new ForkRuntimeHost(runtime, game, bridge, statusPath, log);
    }

    public void WriteStatus(bool sessionStarted, bool connected = true)
    {
        if (string.IsNullOrWhiteSpace(_statusPath))
        {
            return;
        }

        var status = ForkRuntimeStatusBuilder.FromRuntime(_runtime, sessionStarted);
        ForkStatusFile.Write(_statusPath, status, connected, _game);
    }

    public async Task RunDemoLoopAsync(
        double intervalSeconds,
        string? admitUrl,
        CancellationToken cancellationToken)
    {
        _ = admitUrl;
        while (!cancellationToken.IsCancellationRequested)
        {
            WriteStatus(sessionStarted: true);
            await ForkDemoScenarios.RunLoopAsync(_game, _runtime, intervalSeconds, _log, cancellationToken);
            WriteStatus(sessionStarted: true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_bridge is not null)
        {
            await _bridge.DisposeAsync();
        }
    }
}

/// <summary>POST /api/v1/session/admit helper for fork runtimes.</summary>
public static class ForkAdmitClient
{
    public static async Task<bool> TryAdmitAsync(
        string admitUrl,
        string player,
        CancellationToken cancellationToken,
        string? platformUserId = null,
        string? platform = null,
        string? gameId = null,
        string? appId = null)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var payload = JsonSerializer.Serialize(new
        {
            playerId = player,
            displayName = player,
            platformUserId,
            platform,
            gameId,
            appId,
        });

        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(admitUrl, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("admit", out var admitProp) && admitProp.GetBoolean();
    }
}
