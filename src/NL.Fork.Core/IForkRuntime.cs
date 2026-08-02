using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>
/// In-process game fork hook layer (Phase P). Emits <see cref="SessionEvent"/>s, applies NL
/// actions server-side, and validates state before commit — no external RCON/UDP required.
/// </summary>
public interface IForkRuntime
{
    ForkWorldState World { get; }

    IReadOnlyList<ForkAppliedAction> AppliedActions { get; }

    /// <summary>Attempt join after optional admit; spawn denied if NL blocks <c>playerJoin</c>.</summary>
    Task<ForkActionResult> TryJoinAsync(string playerName, CancellationToken cancellationToken = default);

    Task<ForkActionResult> TryShootAsync(string shooter, string target, double damage, CancellationToken cancellationToken = default);

    Task<ForkActionResult> TryMoveAsync(string player, double x, double y, double z, CancellationToken cancellationToken = default);

    Task<ForkActionResult> TryRespawnAsync(string player, CancellationToken cancellationToken = default);

    Task<ForkActionResult> TryChatAsync(string player, string text, CancellationToken cancellationToken = default);

    Task<ForkActionResult> TryLeaveAsync(string player, CancellationToken cancellationToken = default);
}

/// <summary>Result of a propose-then-commit fork action.</summary>
public sealed record ForkActionResult(
    bool Committed,
    Decision Decision,
    string? Message,
    string? ActionVerb = null);

/// <summary>Record of an NL action applied inside the fork.</summary>
public sealed record ForkAppliedAction(
    string Action,
    string Player,
    string Event,
    string Message,
    DateTimeOffset AppliedAtUtc);
