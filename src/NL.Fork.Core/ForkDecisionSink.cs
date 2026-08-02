using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Integration;

namespace NL.Fork.Core;

/// <summary>Evaluates a fork event against NL (embedded engine or remote session bus).</summary>
public interface IForkDecisionSink
{
    Task<ForkDecisionOutcome> EvaluateAsync(SessionEvent sessionEvent, CancellationToken cancellationToken);
}

public sealed record ForkDecisionOutcome(
    Decision Decision,
    string? Message,
    NlActionMessage? Action = null);

/// <summary>In-process <see cref="RuleEngine"/> for tests and embedded fork sessions.</summary>
public sealed class RuleEngineForkDecisionSink : IForkDecisionSink
{
    private readonly RuleEngine _engine;
    private readonly IJoinGate? _joinGate;

    public RuleEngineForkDecisionSink(RuleEngine engine, IJoinGate? joinGate = null)
    {
        _engine = engine;
        _joinGate = joinGate;
    }

    public Task<ForkDecisionOutcome> EvaluateAsync(SessionEvent sessionEvent, CancellationToken cancellationToken)
    {
        var joinOutcome = _joinGate?.TryEvaluate(sessionEvent);
        ActionResult result;
        if (joinOutcome is not null && joinOutcome.ActionResult.Decision == Decision.Block)
        {
            result = joinOutcome.ActionResult;
        }
        else
        {
            result = _engine.Evaluate(sessionEvent.Event);
        }

        NlActionMessage? action = null;
        if (result.Decision == Decision.Block)
        {
            var line = NlActionEnvelope.Serialize(sessionEvent, result);
            action = NlActionEnvelope.TryParse(line);
        }

        return Task.FromResult(new ForkDecisionOutcome(result.Decision, result.Message, action));
    }
}
