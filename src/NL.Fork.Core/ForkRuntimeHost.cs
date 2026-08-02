using System.Text.Json;
using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>Runs hello-fork against a remote NL session bus with optional demo loop.</summary>
public sealed class ForkRuntimeHost : IAsyncDisposable
{
    private readonly HelloForkRuntime _runtime;
    private readonly ForkNlBridgeClient? _bridge;
    private readonly string? _statusPath;
    private readonly Action<string>? _log;

    public ForkRuntimeHost(
        HelloForkRuntime runtime,
        ForkNlBridgeClient? bridge,
        string? statusPath = null,
        Action<string>? log = null)
    {
        _runtime = runtime;
        _bridge = bridge;
        _statusPath = statusPath;
        _log = log;
    }

    public HelloForkRuntime Runtime => _runtime;

    public static ForkRuntimeHost CreateEmbedded(string nleSource, ForkModManifest? mods = null, IJoinGate? joinGate = null)
    {
        var session = new EmbeddedForkSession(nleSource, mods, joinGate);
        return new ForkRuntimeHost(session.Runtime, bridge: null);
    }

    public static ForkRuntimeHost CreateRemote(
        ForkNlBridgeClient bridge,
        ForkModManifest? mods = null,
        Func<string, Task<bool>>? admitAsync = null,
        string? statusPath = null,
        Action<string>? log = null)
    {
        var runtime = new HelloForkRuntime(bridge, mods, admitAsync);
        return new ForkRuntimeHost(runtime, bridge, statusPath, log);
    }

    public void WriteStatus(bool sessionStarted)
    {
        if (string.IsNullOrWhiteSpace(_statusPath))
        {
            return;
        }

        var status = ForkRuntimeStatusBuilder.FromRuntime(_runtime, sessionStarted);
        ForkStatusFile.Write(_statusPath, status);
    }

    public async Task RunDemoLoopAsync(
        double intervalSeconds,
        string? admitUrl,
        CancellationToken cancellationToken)
    {
        _ = admitUrl;
        var runtime = _runtime;
        var players = new[] { "Alice", "Bob" };
        while (!cancellationToken.IsCancellationRequested)
        {
            WriteStatus(sessionStarted: true);

            foreach (var player in players)
            {
                var join = await runtime.TryJoinAsync(player, cancellationToken);
                _log?.Invoke($"[fork] join {player} → {(join.Committed ? "ok" : join.Message)}");
            }

            var shoot = await runtime.TryShootAsync("Alice", "Bob", 12, cancellationToken);
            _log?.Invoke($"[fork] Alice shoots Bob → committed={shoot.Committed} decision={shoot.Decision}");

            if (runtime.World.TryGetPlayer("Bob", out var bob) && bob is not null)
            {
                _log?.Invoke($"[fork] Bob health={bob.Health}");
            }

            await runtime.TryChatAsync("Bob", "HELLO EVERYONE!!!", cancellationToken);
            await runtime.TryRespawnAsync("Bob", cancellationToken);

            foreach (var player in players)
            {
                if (runtime.World.TryGetPlayer(player, out _))
                {
                    await runtime.TryLeaveAsync(player, cancellationToken);
                }
            }

            WriteStatus(sessionStarted: true);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
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
