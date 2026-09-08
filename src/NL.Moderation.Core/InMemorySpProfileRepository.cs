using NL.Core.Sp;

namespace NL.Moderation.Core;

/// <summary>Thread-safe in-memory <see cref="ISpProfileRepository"/> — used by tests and any
/// CLI that doesn't need profiles to survive a restart.</summary>
public sealed class InMemorySpProfileRepository : ISpProfileRepository
{
    private readonly Dictionary<string, SpProfile> _profiles = new();
    private readonly object _lock = new();

    public SpProfile? Find(string playerId)
    {
        lock (_lock)
        {
            return _profiles.TryGetValue(playerId, out var profile) ? profile : null;
        }
    }

    public SpProfile GetOrCreate(string playerId, string displayName)
    {
        lock (_lock)
        {
            if (_profiles.TryGetValue(playerId, out var existing))
            {
                return existing;
            }

            var created = new SpProfile
            {
                Id = playerId,
                DisplayName = displayName,
                AccountCreatedAtUtc = DateTimeOffset.UtcNow,
            };
            _profiles[playerId] = created;
            return created;
        }
    }

    public void Save(SpProfile profile)
    {
        lock (_lock)
        {
            _profiles[profile.Id] = profile;
        }
    }

    public IReadOnlyList<SpProfile> All()
    {
        lock (_lock)
        {
            return _profiles.Values.ToList();
        }
    }
}
