namespace NL.Moderation.Core;

/// <summary>Append-only audit log of <see cref="ModerationRecord"/>s. Implementations: an
/// in-memory store for tests/CLIs, or a real JSON-Lines file store (see
/// <c>NL.Moderation.JsonlModerationStore</c>) for a durable admin/mod dashboard.</summary>
public interface IModerationStore
{
    Task AppendAsync(ModerationRecord record, CancellationToken cancellationToken = default);

    /// <summary>Most recent records for one streamer, newest first, capped at <paramref name="count"/>.</summary>
    Task<IReadOnlyList<ModerationRecord>> GetRecentAsync(
        string streamerId, int count, CancellationToken cancellationToken = default);

    /// <summary>Every record for one specific SP with one streamer, newest first.</summary>
    Task<IReadOnlyList<ModerationRecord>> GetForPlayerAsync(
        string streamerId, string playerId, CancellationToken cancellationToken = default);
}
