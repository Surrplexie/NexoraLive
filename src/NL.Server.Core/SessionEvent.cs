using NL.Core;

namespace NL.Server.Core;

/// <summary>
/// A <see cref="GameEvent"/> ready for <see cref="RuleEngine.Evaluate"/>, plus the player
/// identity that produced it. Player identity is kept alongside the event (not inside it) so
/// <c>GameEvent</c> stays "name + numeric properties only" and Phase 0's
/// <see cref="RuleEngine"/> needs zero changes for any real game source.
/// <see cref="TimestampUtc"/> is optional (Phase 5 anti-cheat uses it for rate/teleport
/// windows when present; otherwise detectors fall back to wall-clock time).
/// </summary>
public sealed record SessionEvent(GameEvent Event, string? PlayerName, DateTimeOffset? TimestampUtc = null);
