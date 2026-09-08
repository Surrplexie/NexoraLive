namespace NL.Kenshi.Bridge;

/// <summary>
/// Community-MP hook catalog. Operators compile this against Kenshi assemblies
/// and call <see cref="Dispatch"/> from patches. The catalog is validated in CI
/// without Kenshi assemblies.
/// </summary>
public static class NlKenshiHarmonyHooks
{
    public const string PackageId = "NexoraLive.NLKenshiBridge";
    public const string SteamAppId = "233860";
    public const int DedicatedPort = 23386;

    /// <summary>Community MP / FCS method names → NL Integration Spec events.</summary>
    public static IReadOnlyDictionary<string, string> HookMap { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Kenshi.GameWorld.Start"] = "sessionStart",
        ["Kenshi.Character.Destroy"] = "playerLeave",
        ["Kenshi.Chat.Message"] = "playerChat",
        ["Kenshi.Combat.Hit"] = "entityDamage",
        ["Kenshi.Building.Destroy"] = "buildingDestroy",
        ["Kenshi.Inventory.Steal"] = "steal",
        ["Kenshi.Squad.GiveOrder"] = "squadOrder",
        ["Kenshi.Faction.StartRaid"] = "raid",
        ["Kenshi.Trade.Complete"] = "trade",
        ["Kenshi.Item.Destroy"] = "itemDestroy",
        ["Kenshi.Character.KnockedOut"] = "knockdown",
        ["KenshiMP.Network.OnPlayerJoin"] = "playerJoin",
        ["KenshiMP.Network.OnPlayerLeave"] = "playerLeave",
        ["KenshiMP.Chat.OnMessage"] = "playerChat",
    };

    public static IReadOnlyList<string> RequiredEvents => NlKenshiModHost.RequiredEvents;

    public static IReadOnlyList<string> RequiredActions => NlKenshiModHost.RequiredActions;

    public static bool TryResolveHook(string typeMethod, out string nlEvent) =>
        HookMap.TryGetValue(typeMethod, out nlEvent!);

    public static NlKenshiDecision Dispatch(
        NlKenshiModHost host,
        string typeMethod,
        string player,
        IReadOnlyDictionary<string, double>? props = null)
    {
        if (!TryResolveHook(typeMethod, out var nlEvent))
        {
            return NlKenshiDecision.Allow();
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
            "steal" => host.TrySteal(player, props.GetValueOrDefault("steal.value")),
            "squadOrder" => host.TrySquadOrder(player, props.GetValueOrDefault("squad.ordered") > 0),
            "raid" => host.TryRaid(player, props.GetValueOrDefault("raid.severity")),
            "trade" => host.TryTrade(player, props.GetValueOrDefault("trade.value")),
            "itemDestroy" => host.TryItemDestroy(player, props.GetValueOrDefault("item.value")),
            _ => NlKenshiDecision.Allow(),
        };
    }
}
