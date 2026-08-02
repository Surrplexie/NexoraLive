namespace NL.Fork.Core;

/// <summary>Authoritative player state on an NL-hosted fork (server-side only).</summary>
public sealed class ForkPlayerState
{
    public required string Name { get; init; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public double Health { get; set; } = 100;

    public bool Alive => Health > 0;

    public bool HasWeapon { get; set; } = true;

    public bool Connected { get; set; } = true;

    public DateTimeOffset JoinedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public ForkPlayerState Clone() => new()
    {
        Name = Name,
        X = X,
        Y = Y,
        Z = Z,
        Health = Health,
        HasWeapon = HasWeapon,
        Connected = Connected,
        JoinedAtUtc = JoinedAtUtc,
    };
}

/// <summary>All connected players on one fork instance.</summary>
public sealed class ForkWorldState
{
    private readonly Dictionary<string, ForkPlayerState> _players = new(StringComparer.OrdinalIgnoreCase);

    public double BoundaryMin { get; set; } = -500;

    public double BoundaryMax { get; set; } = 500;

    public IReadOnlyDictionary<string, ForkPlayerState> Players => _players;

    public bool TryGetPlayer(string name, out ForkPlayerState? player) =>
        _players.TryGetValue(name, out player);

    public ForkPlayerState AddPlayer(string name)
    {
        var player = new ForkPlayerState { Name = name };
        _players[name] = player;
        return player;
    }

    public bool RemovePlayer(string name) => _players.Remove(name);

    public int ConnectedCount => _players.Values.Count(p => p.Connected);
}
