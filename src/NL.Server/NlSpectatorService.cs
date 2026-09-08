using System.Net.WebSockets;
using System.Text;
using NL.Moderation;
using NL.Moderation.Core;
using NL.Server.Core.Integration;

namespace NL.Server;

public sealed class NlSpectatorService
{
    private readonly NlSpectatorSettings _settings;
    private readonly NlSpectatorRateLimiter _rateLimiter;

    public NlSpectatorService(NlSpectatorSettings settings)
    {
        _settings = settings;
        _rateLimiter = new NlSpectatorRateLimiter(settings.TriggerRatePerMinute);
    }

    public object BuildStatus(
        SessionHostState state,
        bool sessionRunning,
        int decisions,
        bool demoEnabled,
        SessionProfileFile profile) => new
    {
        sessionRunning,
        state = state.ToString(),
        decisions,
        demoEnabled,
        streamerId = profile.StreamerId,
        joinGateEnabled = profile.JoinGate,
        triggersEnabled = _settings.TriggersEnabled,
    };

    public object ListScenarios() => new
    {
        scenarios = NlSpectatorScenarios.List().Select(s => new
        {
            s.Id,
            s.Label,
            s.Description,
            s.Event,
            s.ExpectedDecision,
        }),
    };

    public async Task<IReadOnlyList<SpectatorDecisionView>> GetDecisionsAsync(
        ModerationHostState moderation,
        string streamerId,
        DateTimeOffset? sinceUtc,
        int? count,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(count ?? _settings.FeedMaxCount, 1, _settings.FeedMaxCount);
        var records = await moderation.Moderation.GetRecentActionsAsync(streamerId, take, cancellationToken);

        IEnumerable<ModerationRecord> filtered = records
            .Where(r => r.Kind == ModerationActionKind.AutomaticDecision);

        if (sinceUtc is not null)
        {
            filtered = filtered.Where(r => r.TimestampUtc > sinceUtc);
        }

        return filtered
            .OrderByDescending(r => r.TimestampUtc)
            .Take(take)
            .Select(SpectatorDecisionView.FromRecord)
            .ToList();
    }

    public async Task<SpectatorTriggerResult> TriggerScenarioAsync(
        string scenarioId,
        string clientKey,
        bool sessionRunning,
        string bindHost,
        int wsPort,
        string busToken,
        CancellationToken cancellationToken)
    {
        if (!_settings.TriggersEnabled)
        {
            return SpectatorTriggerResult.Fail(503, "Spectator triggers are disabled.");
        }

        if (!sessionRunning)
        {
            return SpectatorTriggerResult.Fail(503, "Session is not running. Wait for the demo loop to start.");
        }

        var scenario = NlSpectatorScenarios.Find(scenarioId);
        if (scenario is null)
        {
            return SpectatorTriggerResult.Fail(400, $"Unknown scenario '{scenarioId}'.");
        }

        if (!_rateLimiter.TryAcquire(clientKey))
        {
            return SpectatorTriggerResult.Fail(429, "Rate limit exceeded. Try again in a minute.");
        }

        var line = NlSpectatorScenarios.ToNdjsonLine(scenario);
        try
        {
            await InjectEventLineAsync(bindHost, wsPort, busToken, line, cancellationToken);
        }
        catch (Exception ex)
        {
            return SpectatorTriggerResult.Fail(503, $"Could not inject event: {ex.Message}");
        }

        return SpectatorTriggerResult.Success(new
        {
            ok = true,
            scenarioId = scenario.Id,
            eventName = scenario.Event,
            player = scenario.Player,
            expectedDecision = scenario.ExpectedDecision,
        });
    }

    internal static async Task InjectEventLineAsync(
        string bindHost,
        int wsPort,
        string busToken,
        string line,
        CancellationToken cancellationToken)
    {
        var connectHost = bindHost is "0.0.0.0" or "+" or "*" ? "127.0.0.1" : bindHost;
        var uri = new Uri(
            $"ws://{connectHost}:{wsPort}{NlIntegrationProtocol.WebSocketPath}?token={Uri.EscapeDataString(busToken)}");

        using var ws = new ClientWebSocket();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        await ws.ConnectAsync(uri, timeout.Token);

        var payload = Encoding.UTF8.GetBytes(line);
        await ws.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);

        if (ws.State == WebSocketState.Open)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "spectator-trigger", timeout.Token);
        }
    }
}

public sealed record SpectatorTriggerResult(int StatusCode, object Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public static SpectatorTriggerResult Success(object body) => new(200, body);

    public static SpectatorTriggerResult Fail(int statusCode, string error) =>
        new(statusCode, new { error });
}

public sealed class SpectatorDecisionView
{
    public required Guid Id { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required string PlayerName { get; init; }

    public required string EventName { get; init; }

    public required string Decision { get; init; }

    public string? Message { get; init; }

    public static SpectatorDecisionView FromRecord(ModerationRecord record) => new()
    {
        Id = record.Id,
        TimestampUtc = record.TimestampUtc,
        PlayerName = record.PlayerName ?? record.PlayerId ?? "?",
        EventName = record.EventName,
        Decision = record.Decision?.ToString() ?? "?",
        Message = record.Message,
    };
}

public sealed class SpectatorTriggerRequest
{
    public string ScenarioId { get; set; } = "";
}
