using NL.Core;

namespace NL.Server.Core;

/// <summary>
/// Game-agnostic NLServer loop: pull <see cref="SessionEvent"/>s from any
/// <see cref="IGameEventSource"/>, optionally join-gate <c>playerJoin</c>, evaluate remaining
/// events with a <see cref="RuleEngine"/>, and hand Block (or optionally all) decisions to an
/// <see cref="IGameActionSink"/>. No Minecraft- or file-specific code lives here.
/// </summary>
public sealed class NlServerHost
{
    private readonly RuleEngine _engine;
    private readonly IGameEventSource _source;
    private readonly IGameActionSink _sink;
    private readonly IJoinGate? _joinGate;
    private readonly bool _applyOnAllow;

    public NlServerHost(
        RuleEngine engine,
        IGameEventSource source,
        IGameActionSink sink,
        IJoinGate? joinGate = null,
        bool applyOnAllow = false)
    {
        _engine = engine;
        _source = source;
        _sink = sink;
        _joinGate = joinGate;
        _applyOnAllow = applyOnAllow;
    }

    public IReadOnlyList<HostedDecision> Decisions { get; private set; } = Array.Empty<HostedDecision>();

    /// <summary>Runs until the source ends or <paramref name="cancellationToken"/> fires.
    /// <paramref name="onDecision"/> is awaited (e.g. Phase 4's moderation audit log) before
    /// the action sink runs, so a logging failure never suppresses a real action.</summary>
    public async Task RunAsync(CancellationToken cancellationToken, Func<HostedDecision, Task>? onDecision = null)
    {
        var recorded = new List<HostedDecision>();

        await foreach (var sessionEvent in _source.ReadEventsAsync(cancellationToken))
        {
            JoinGateOutcome? joinOutcome = _joinGate?.TryEvaluate(sessionEvent);
            ActionResult result;

            if (joinOutcome is not null && joinOutcome.ActionResult.Decision == Decision.Block)
            {
                // Deny / Hold: kick (or dry-run) and do not run normal join rules.
                result = joinOutcome.ActionResult;
            }
            else
            {
                result = _engine.Evaluate(sessionEvent.Event);
            }

            var hosted = new HostedDecision(sessionEvent, result, joinOutcome);
            recorded.Add(hosted);

            if (onDecision is not null)
            {
                await onDecision(hosted);
            }

            var shouldApply = result.Decision == Decision.Block
                || (_applyOnAllow && result.Decision == Decision.Allow);

            if (shouldApply)
            {
                await _sink.ApplyAsync(sessionEvent, result, cancellationToken);
            }
        }

        Decisions = recorded;
    }
}

/// <summary>One evaluated session event, kept for tests and CLI summaries.</summary>
public sealed record HostedDecision(
    SessionEvent SessionEvent,
    ActionResult Result,
    JoinGateOutcome? JoinGate = null);
