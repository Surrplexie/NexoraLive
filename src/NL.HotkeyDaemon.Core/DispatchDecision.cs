namespace NL.HotkeyDaemon.Core;

public enum DispatchOutcome
{
    Perform,
    Skip,
}

/// <summary>What <see cref="ActionDispatcher"/> decided for one action name: whether the real
/// handler should run, plus a human-readable reason/message for logging or notifications.</summary>
public sealed record DispatchDecision(DispatchOutcome Outcome, string Action, string? Message)
{
    public bool ShouldPerform => Outcome == DispatchOutcome.Perform;
}
