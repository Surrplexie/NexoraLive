namespace NL.Fork.Core;

/// <summary>Summary of fork world state for operator / API dashboards.</summary>
public sealed record ForkRuntimeStatus(
    bool SessionStarted,
    int ConnectedPlayers,
    IReadOnlyList<ForkPlayerStatus> Players,
    IReadOnlyList<ForkAppliedAction> RecentActions,
    IReadOnlyList<string> LoadedModIds);

public sealed record ForkPlayerStatus(
    string Name,
    double Health,
    bool Alive,
    bool HasWeapon,
    double X,
    double Y,
    double Z);

/// <summary>Builds status snapshots from a live fork runtime.</summary>
public static class ForkRuntimeStatusBuilder
{
    public static ForkRuntimeStatus FromRuntime(HelloForkRuntime runtime, bool sessionStarted)
    {
        var players = runtime.World.Players.Values
            .Select(p => new ForkPlayerStatus(p.Name, p.Health, p.Alive, p.HasWeapon, p.X, p.Y, p.Z))
            .ToList();

        var modIds = runtime.Mods.Mods.Select(m => m.Id).ToList();

        return new ForkRuntimeStatus(
            sessionStarted,
            runtime.World.ConnectedCount,
            players,
            runtime.AppliedActions.TakeLast(20).ToList(),
            modIds);
    }
}
