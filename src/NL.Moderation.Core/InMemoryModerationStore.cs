namespace NL.Moderation.Core;

/// <summary>Thread-safe in-memory <see cref="IModerationStore"/> — used by tests and any CLI
/// that doesn't need the log to survive a restart.</summary>
public sealed class InMemoryModerationStore : IModerationStore
{
    private readonly List<ModerationRecord> _records = new();
    private readonly object _lock = new();

    public Task AppendAsync(ModerationRecord record, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModerationRecord>> GetRecentAsync(
        string streamerId, int count, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<ModerationRecord> result = _records
                .Where(r => r.StreamerId == streamerId)
                .OrderByDescending(r => r.TimestampUtc)
                .Take(count)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<ModerationRecord>> GetForPlayerAsync(
        string streamerId, string playerId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<ModerationRecord> result = _records
                .Where(r => r.StreamerId == streamerId && r.PlayerId == playerId)
                .OrderByDescending(r => r.TimestampUtc)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
