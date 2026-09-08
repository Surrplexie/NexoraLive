namespace NL.RimWorld.Bridge;

/// <summary>
/// In-process colony host: maps RimWorld Together / Harmony events onto NL propose-then-commit.
/// Kick removes the colonist from the roster; warn/tell are recorded for overlay / chat.
/// </summary>
public sealed class NlRimWorldModHost
{
    public static readonly string[] RequiredEvents =
    [
        "sessionStart", "playerJoin", "playerLeave", "playerChat",
        "entityDamage", "buildingDestroy", "zoneEdit", "animalRelease",
        "colonistDraft", "trade", "itemDestroy", "colonistDown",
    ];

    public static readonly string[] RequiredActions = ["warn", "kick", "tell"];

    private readonly INlRimWorldDecisionBus _bus;
    private readonly Dictionary<string, ColonyPawn> _pawns = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NlRimWorldAction> _applied = [];
    private bool _sessionStarted;

    public NlRimWorldModHost(INlRimWorldDecisionBus bus) =>
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    public IReadOnlyCollection<string> ConnectedPawns => _pawns.Keys;

    public IReadOnlyList<NlRimWorldAction> AppliedActions => _applied;

    public bool SessionStarted => _sessionStarted;

    public NlRimWorldDecision StartSession()
    {
        var decision = _bus.Evaluate("sessionStart", "system", new Dictionary<string, double> { ["map.id"] = 1 });
        if (decision.Allowed)
        {
            _sessionStarted = true;
        }

        return decision;
    }

    public NlRimWorldDecision TryJoin(string player)
    {
        EnsureSession();
        if (_pawns.ContainsKey(player))
        {
            return NlRimWorldDecision.Block("already joined", "kick");
        }

        var decision = _bus.Evaluate("playerJoin", player, new Dictionary<string, double> { ["player.alive"] = 1 });
        if (decision.Allowed)
        {
            _pawns[player] = new ColonyPawn(player);
        }

        ApplyIfNeeded(decision, player, "playerJoin");
        return decision;
    }

    public NlRimWorldDecision TryChat(string player, string text)
    {
        if (!_pawns.ContainsKey(player))
        {
            return NlRimWorldDecision.Block("unknown player");
        }

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
        var decision = _bus.Evaluate("playerChat", player, new Dictionary<string, double>
        {
            ["chat.length"] = text.Length,
            ["chat.capsRatio"] = capsRatio,
            ["chat.isCommand"] = text.StartsWith('/') ? 1 : 0,
        });
        ApplyIfNeeded(decision, player, "playerChat");
        return decision;
    }

    public NlRimWorldDecision TryDamage(string attacker, double damage)
    {
        if (!_pawns.ContainsKey(attacker))
        {
            return NlRimWorldDecision.Block("unknown player");
        }

        var decision = _bus.Evaluate("entityDamage", attacker, new Dictionary<string, double>
        {
            ["weapon.damage"] = damage,
            ["player.alive"] = 1,
        });
        ApplyIfNeeded(decision, attacker, "entityDamage");
        return decision;
    }

    public NlRimWorldDecision TryBuildingDestroy(string player, double value) =>
        Propose(player, "buildingDestroy", new Dictionary<string, double> { ["building.value"] = value });

    public NlRimWorldDecision TryZoneEdit(string player, double tiles) =>
        Propose(player, "zoneEdit", new Dictionary<string, double> { ["zone.tiles"] = tiles });

    public NlRimWorldDecision TryAnimalRelease(string player, double count) =>
        Propose(player, "animalRelease", new Dictionary<string, double> { ["animal.count"] = count });

    public NlRimWorldDecision TryDraft(string player, bool drafted) =>
        Propose(player, "colonistDraft", new Dictionary<string, double> { ["colonist.drafted"] = drafted ? 1 : 0 });

    public NlRimWorldDecision TryTrade(string player, double value) =>
        Propose(player, "trade", new Dictionary<string, double> { ["trade.value"] = value });

    public NlRimWorldDecision TryItemDestroy(string player, double value) =>
        Propose(player, "itemDestroy", new Dictionary<string, double> { ["item.value"] = value });

    public NlRimWorldDecision TryLeave(string player)
    {
        var decision = Propose(player, "playerLeave", new Dictionary<string, double> { ["player.alive"] = 1 });
        _pawns.Remove(player);
        return decision;
    }

    public void Apply(NlRimWorldAction action)
    {
        _applied.Add(action);
        if (string.Equals(action.Action, "kick", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action.Action, "despawn", StringComparison.OrdinalIgnoreCase))
        {
            _pawns.Remove(action.Player);
        }
    }

    private NlRimWorldDecision Propose(string player, string eventName, Dictionary<string, double> props)
    {
        if (!_pawns.ContainsKey(player) && eventName is not "playerJoin" and not "sessionStart")
        {
            return NlRimWorldDecision.Block("unknown player");
        }

        var decision = _bus.Evaluate(eventName, player, props);
        ApplyIfNeeded(decision, player, eventName);
        return decision;
    }

    private void ApplyIfNeeded(NlRimWorldDecision decision, string player, string eventName)
    {
        if (decision.Allowed || string.IsNullOrWhiteSpace(decision.Action))
        {
            return;
        }

        Apply(new NlRimWorldAction(decision.Action, player, eventName, "Block", decision.Message ?? ""));
    }

    private void EnsureSession()
    {
        if (!_sessionStarted)
        {
            StartSession();
        }
    }

    private sealed record ColonyPawn(string Name);
}

/// <summary>Deterministic in-memory bus for unit tests (no session host required).</summary>
public sealed class NlRimWorldRuleBus : INlRimWorldDecisionBus
{
    private readonly Func<string, string, IReadOnlyDictionary<string, double>, NlRimWorldDecision> _evaluate;

    public NlRimWorldRuleBus(Func<string, string, IReadOnlyDictionary<string, double>, NlRimWorldDecision> evaluate) =>
        _evaluate = evaluate;

    public NlRimWorldDecision Evaluate(string eventName, string player, IReadOnlyDictionary<string, double> props) =>
        _evaluate(eventName, player, props);

    public static NlRimWorldRuleBus FromColonyDefaults() => new((evt, _, props) =>
    {
        if (evt == "playerChat" && props.GetValueOrDefault("chat.capsRatio") > 0.8
            && props.GetValueOrDefault("chat.length") > 10)
        {
            return NlRimWorldDecision.Block("please avoid excessive caps in colony chat", "warn");
        }

        if (evt == "entityDamage" && props.GetValueOrDefault("weapon.damage") > 40)
        {
            return NlRimWorldDecision.Block("excessive pawn damage", "kick");
        }

        if (evt == "buildingDestroy" && props.GetValueOrDefault("building.value") > 1500)
        {
            return NlRimWorldDecision.Block("high-value building destroy blocked", "kick");
        }

        if (evt == "zoneEdit" && props.GetValueOrDefault("zone.tiles") > 80)
        {
            return NlRimWorldDecision.Block("mass zone edit blocked", "warn");
        }

        if (evt == "animalRelease" && props.GetValueOrDefault("animal.count") > 3)
        {
            return NlRimWorldDecision.Block("mass animal release blocked", "kick");
        }

        if (evt == "trade" && props.GetValueOrDefault("trade.value") > 5000)
        {
            return NlRimWorldDecision.Block("high-value trade blocked", "warn");
        }

        if (evt == "itemDestroy" && props.GetValueOrDefault("item.value") > 800)
        {
            return NlRimWorldDecision.Block("high-value item destroy blocked", "kick");
        }

        return NlRimWorldDecision.Allow();
    });
}
