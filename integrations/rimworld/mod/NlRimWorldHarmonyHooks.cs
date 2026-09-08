namespace NL.RimWorld.Bridge;

/// <summary>
/// Harmony / RimWorld Together hook catalog. Operators compile this against RimWorld 1.5
/// (net472 + Harmony) and call <see cref="Dispatch"/> from patches. The catalog is
/// validated in CI without RimWorld assemblies.
/// </summary>
public static class NlRimWorldHarmonyHooks
{
    public const string PackageId = "NexoraLive.NLBridge";
    public const string SteamAppId = "294100";
    public const int DedicatedPort = 25555;

    /// <summary>Together / vanilla method names → NL Integration Spec events.</summary>
    public static IReadOnlyDictionary<string, string> HookMap { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Verse.Game.FinalizeInit"] = "sessionStart",
        ["RimWorld.GameComponent.StartedNewGame"] = "sessionStart",
        ["Verse.Pawn.Destroy"] = "playerLeave",
        ["RimWorld.PlayLog.Add"] = "playerChat",
        ["Verse.DamageWorker.Apply"] = "entityDamage",
        ["RimWorld.Building.Destroy"] = "buildingDestroy",
        ["RimWorld.Zone.AddCell"] = "zoneEdit",
        ["RimWorld.Pawn_PlayerSettings.ReleaseAnimals"] = "animalRelease",
        ["RimWorld.Pawn_DraftController.set_Drafted"] = "colonistDraft",
        ["RimWorld.TradeDeal.TryExecute"] = "trade",
        ["Verse.Thing.Destroy"] = "itemDestroy",
        ["Verse.Pawn_HealthTracker.MakeDowned"] = "colonistDown",
        ["RimWorldOnline.Server.PlayerJoin"] = "playerJoin",
        ["RimWorldTogether.Network.OnPlayerJoin"] = "playerJoin",
        ["RimWorldTogether.Network.OnPlayerLeave"] = "playerLeave",
        ["RimWorldTogether.Chat.OnMessage"] = "playerChat",
    };

    public static IReadOnlyList<string> RequiredEvents => NlRimWorldModHost.RequiredEvents;

    public static IReadOnlyList<string> RequiredActions => NlRimWorldModHost.RequiredActions;

    public static bool TryResolveHook(string typeMethod, out string nlEvent) =>
        HookMap.TryGetValue(typeMethod, out nlEvent!);

    public static NlRimWorldDecision Dispatch(
        NlRimWorldModHost host,
        string typeMethod,
        string player,
        IReadOnlyDictionary<string, double>? props = null)
    {
        if (!TryResolveHook(typeMethod, out var nlEvent))
        {
            return NlRimWorldDecision.Allow();
        }

        props ??= new Dictionary<string, double>();
        return nlEvent switch
        {
            "sessionStart" => host.StartSession(),
            "playerJoin" => host.TryJoin(player),
            "playerLeave" => host.TryLeave(player),
            "playerChat" => host.TryChat(player, props.GetValueOrDefault("chat.length") > 0 ? "x" : "ok"),
            "entityDamage" => host.TryDamage(player, props.GetValueOrDefault("weapon.damage", 1)),
            "buildingDestroy" => host.TryBuildingDestroy(player, props.GetValueOrDefault("building.value")),
            "zoneEdit" => host.TryZoneEdit(player, props.GetValueOrDefault("zone.tiles")),
            "animalRelease" => host.TryAnimalRelease(player, props.GetValueOrDefault("animal.count")),
            "colonistDraft" => host.TryDraft(player, props.GetValueOrDefault("colonist.drafted") > 0),
            "trade" => host.TryTrade(player, props.GetValueOrDefault("trade.value")),
            "itemDestroy" => host.TryItemDestroy(player, props.GetValueOrDefault("item.value")),
            _ => NlRimWorldDecision.Allow(),
        };
    }
}
