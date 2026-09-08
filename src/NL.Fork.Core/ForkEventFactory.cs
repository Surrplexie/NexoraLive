using NL.Core;
using NL.Server.Core;
using NL.Server.Core.Integration;

namespace NL.Fork.Core;

/// <summary>Builds rich <see cref="SessionEvent"/> payloads from fork world state.</summary>
public static class ForkEventFactory
{
    public static SessionEvent PlayerJoin(ForkPlayerState player, ForkModManifest mods) =>
        WithProps("playerJoin", player.Name, mods, new Dictionary<string, double>
        {
            ["player.alive"] = player.Alive ? 1 : 0,
            ["player.health"] = player.Health,
            ["player.x"] = player.X,
            ["player.y"] = player.Y,
            ["player.z"] = player.Z,
        });

    public static SessionEvent Shoot(
        ForkPlayerState shooter,
        ForkPlayerState? target,
        double damage,
        ForkModManifest mods) =>
        WithProps("shoot", shooter.Name, mods, new Dictionary<string, double>
        {
            ["weapon.damage"] = damage,
            ["player.alive"] = shooter.Alive ? 1 : 0,
            ["target.alive"] = target?.Alive == true ? 1 : 0,
            ["player.x"] = shooter.X,
            ["player.y"] = shooter.Y,
            ["player.z"] = shooter.Z,
        });

    public static SessionEvent Move(ForkPlayerState player, ForkModManifest mods) =>
        WithProps("move", player.Name, mods, new Dictionary<string, double>
        {
            ["player.x"] = player.X,
            ["player.y"] = player.Y,
            ["player.z"] = player.Z,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent Respawn(ForkPlayerState player, double health, ForkModManifest mods) =>
        WithProps("respawn", player.Name, mods, new Dictionary<string, double>
        {
            ["player.health"] = health,
            ["player.alive"] = health > 0 ? 1 : 0,
        });

    public static SessionEvent PlayerChat(ForkPlayerState player, string text, ForkModManifest mods)
    {
        var caps = 0;
        var letters = 0;
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                letters++;
                if (char.IsUpper(ch))
                {
                    caps++;
                }
            }
        }

        var capsRatio = letters > 0 ? (double)caps / letters : 0;
        return WithProps("playerChat", player.Name, mods, new Dictionary<string, double>
        {
            ["chat.length"] = text.Length,
            ["chat.capsRatio"] = capsRatio,
            ["chat.isCommand"] = text.StartsWith('/') ? 1 : 0,
        });
    }

    public static SessionEvent PlayerLeave(ForkPlayerState player, ForkModManifest mods) =>
        WithProps("playerLeave", player.Name, mods, new Dictionary<string, double>
        {
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent SessionStart(ForkModManifest mods) =>
        WithProps("sessionStart", "NL-Fork", mods, new Dictionary<string, double>
        {
            ["map.id"] = 1,
        });

    public static SessionEvent LeaveBoundary(ForkPlayerState player, ForkModManifest mods) =>
        WithProps("leaveBoundary", player.Name, mods, new Dictionary<string, double>
        {
            ["player.x"] = player.X,
            ["player.y"] = player.Y,
            ["player.z"] = player.Z,
        });

    public static SessionEvent PlayerDeath(ForkPlayerState player, ForkModManifest mods, double sessionDeathCount = 1) =>
        WithProps("playerDeath", player.Name, mods, new Dictionary<string, double>
        {
            ["player.alive"] = 0,
            ["player.health"] = 0,
            ["player.sessionDeathCount"] = sessionDeathCount,
        });

    public static SessionEvent EntityDamage(
        ForkPlayerState attacker,
        ForkPlayerState? victim,
        double damage,
        ForkModManifest mods) =>
        WithProps("entityDamage", attacker.Name, mods, new Dictionary<string, double>
        {
            ["weapon.damage"] = damage,
            ["player.alive"] = attacker.Alive ? 1 : 0,
            ["target.alive"] = victim?.Alive == true ? 1 : 0,
        });

    public static SessionEvent PlayerAdvancement(ForkPlayerState player, string advancementId, ForkModManifest mods) =>
        WithProps("playerAdvancement", player.Name, mods, new Dictionary<string, double>
        {
            ["advancement.id"] = advancementId.GetHashCode(StringComparison.Ordinal) & 0xFFFF,
        });

    public static SessionEvent SessionEnd(ForkModManifest mods) =>
        WithProps("sessionEnd", "NL-Fork", mods, new Dictionary<string, double>());

    public static SessionEvent BeamngMove(ForkPlayerState player, double speedMph, ForkModManifest mods) =>
        WithProps("move", player.Name, mods, new Dictionary<string, double>
        {
            ["vehicle.speed"] = speedMph,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent Crash(ForkPlayerState player, double severity, ForkModManifest mods) =>
        WithProps("crash", player.Name, mods, new Dictionary<string, double>
        {
            ["crash.severity"] = severity,
            ["vehicle.speed"] = player.X,
        });

    public static SessionEvent Airtime(ForkPlayerState player, double seconds, ForkModManifest mods) =>
        WithProps("airtime", player.Name, mods, new Dictionary<string, double>
        {
            ["airtime.seconds"] = seconds,
        });

    public static SessionEvent Rollover(ForkPlayerState player, ForkModManifest mods) =>
        WithProps("rollover", player.Name, mods, new Dictionary<string, double>
        {
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent BuildingDestroy(ForkPlayerState player, double value, ForkModManifest mods) =>
        WithProps("buildingDestroy", player.Name, mods, new Dictionary<string, double>
        {
            ["building.value"] = value,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent ZoneEdit(ForkPlayerState player, double tiles, ForkModManifest mods) =>
        WithProps("zoneEdit", player.Name, mods, new Dictionary<string, double>
        {
            ["zone.tiles"] = tiles,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent AnimalRelease(ForkPlayerState player, double count, ForkModManifest mods) =>
        WithProps("animalRelease", player.Name, mods, new Dictionary<string, double>
        {
            ["animal.count"] = count,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent ColonistDraft(ForkPlayerState player, bool drafted, ForkModManifest mods) =>
        WithProps("colonistDraft", player.Name, mods, new Dictionary<string, double>
        {
            ["colonist.drafted"] = drafted ? 1 : 0,
            ["player.alive"] = player.Alive ? 1 : 0,
            ["player.downed"] = player.Downed ? 1 : 0,
        });

    public static SessionEvent Trade(ForkPlayerState player, double value, ForkModManifest mods) =>
        WithProps("trade", player.Name, mods, new Dictionary<string, double>
        {
            ["trade.value"] = value,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent ItemDestroy(ForkPlayerState player, double value, ForkModManifest mods) =>
        WithProps("itemDestroy", player.Name, mods, new Dictionary<string, double>
        {
            ["item.value"] = value,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent ColonistDown(ForkPlayerState player, ForkModManifest mods) =>
        WithProps("colonistDown", player.Name, mods, new Dictionary<string, double>
        {
            ["player.alive"] = 0,
            ["player.downed"] = 1,
            ["player.health"] = player.Health,
        });

    public static SessionEvent Steal(ForkPlayerState player, double value, ForkModManifest mods) =>
        WithProps("steal", player.Name, mods, new Dictionary<string, double>
        {
            ["steal.value"] = value,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent SquadOrder(ForkPlayerState player, bool ordered, ForkModManifest mods) =>
        WithProps("squadOrder", player.Name, mods, new Dictionary<string, double>
        {
            ["squad.ordered"] = ordered ? 1 : 0,
            ["player.alive"] = player.Alive ? 1 : 0,
            ["player.downed"] = player.Downed ? 1 : 0,
        });

    public static SessionEvent Raid(ForkPlayerState player, double severity, ForkModManifest mods) =>
        WithProps("raid", player.Name, mods, new Dictionary<string, double>
        {
            ["raid.severity"] = severity,
            ["player.alive"] = player.Alive ? 1 : 0,
        });

    public static SessionEvent Knockdown(ForkPlayerState player, ForkModManifest mods) =>
        WithProps("knockdown", player.Name, mods, new Dictionary<string, double>
        {
            ["player.alive"] = 0,
            ["player.downed"] = 1,
            ["player.health"] = player.Health,
        });

    private static SessionEvent WithProps(
        string eventName,
        string? player,
        ForkModManifest mods,
        Dictionary<string, double> props)
    {
        var merged = ForkModLoader.ApplyMods(mods, props);
        return new SessionEvent(new GameEvent(eventName, merged), player, DateTimeOffset.UtcNow);
    }
}
