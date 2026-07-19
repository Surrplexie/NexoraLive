namespace NL.Server.Core;

/// <summary>
/// Tracks simple per-player running counts across a server session (join count, death count,
/// advancement count) so <see cref="MinecraftEventMapper"/> can attach numeric properties to
/// the <c>GameEvent</c>s it produces, matching the NL.Core convention of numeric-only
/// per-event properties (see docs/ARCHITECTURE.md). Pure in-memory state, no I/O.
/// </summary>
public sealed class PlayerSessionTracker
{
    private readonly Dictionary<string, PlayerStats> _stats = new();

    public PlayerStats GetStats(string playerName) =>
        _stats.TryGetValue(playerName, out var stats) ? stats : PlayerStats.Empty;

    public PlayerStats RecordJoin(string playerName) => Update(playerName, s => s with { JoinCount = s.JoinCount + 1 });

    public PlayerStats RecordDeath(string playerName) => Update(playerName, s => s with { DeathCount = s.DeathCount + 1 });

    public PlayerStats RecordAdvancement(string playerName) =>
        Update(playerName, s => s with { AdvancementCount = s.AdvancementCount + 1 });

    private PlayerStats Update(string playerName, Func<PlayerStats, PlayerStats> mutate)
    {
        var updated = mutate(GetStats(playerName));
        _stats[playerName] = updated;
        return updated;
    }
}

/// <summary>Running per-player counters for one server session.</summary>
public readonly record struct PlayerStats(int JoinCount, int DeathCount, int AdvancementCount)
{
    public static readonly PlayerStats Empty = new(0, 0, 0);
}
