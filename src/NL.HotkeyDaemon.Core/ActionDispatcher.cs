using NL.Core;

namespace NL.HotkeyDaemon.Core;

/// <summary>
/// The pure decision logic that sits between a hotkey press and a real action handler: given
/// an action name, asks the existing <see cref="RuleEngine"/> whether it's currently allowed,
/// and tracks the "toggleNlEvents" master on/off switch described in nl.txt's Feature 1
/// ("enable/disable NLEvents" as a recommended shortcut). No Win32/UI/audio code lives here so
/// it stays unit-testable like the rest of NL.Core.
/// </summary>
public sealed class ActionDispatcher
{
    public const string ToggleNlEventsAction = "toggleNlEvents";

    private readonly RuleEngine _engine;

    public ActionDispatcher(RuleEngine engine, bool initiallyEnabled = true)
    {
        _engine = engine;
        Enabled = initiallyEnabled;
    }

    /// <summary>Whether hotkey-triggered actions (other than the toggle itself) currently run.</summary>
    public bool Enabled { get; private set; }

    public DispatchDecision Dispatch(string action)
    {
        if (string.Equals(action, ToggleNlEventsAction, StringComparison.OrdinalIgnoreCase))
        {
            // The master switch always gets evaluated, even while disabled - otherwise a
            // streamer could lock themselves out of ever re-enabling it.
            return DispatchToggle(action);
        }

        if (!Enabled)
        {
            return new DispatchDecision(DispatchOutcome.Skip, action, "NLEvents is currently disabled");
        }

        var result = _engine.Evaluate(GameEvent.Simple(action));
        return result.Decision == Decision.Allow
            ? new DispatchDecision(DispatchOutcome.Perform, action, result.Message)
            : new DispatchDecision(DispatchOutcome.Skip, action, result.Message ?? "blocked by an NLEvents rule");
    }

    private DispatchDecision DispatchToggle(string action)
    {
        var result = _engine.Evaluate(GameEvent.Simple(action));
        if (result.Decision != Decision.Allow)
        {
            return new DispatchDecision(DispatchOutcome.Skip, action, result.Message ?? "blocked by an NLEvents rule");
        }

        Enabled = !Enabled;
        return new DispatchDecision(DispatchOutcome.Perform, action, Enabled ? "NLEvents enabled" : "NLEvents disabled");
    }
}
