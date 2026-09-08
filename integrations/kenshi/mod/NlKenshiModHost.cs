namespace NL.Kenshi.Bridge;

/// <summary>
/// In-process outpost host: maps Kenshi community-MP events onto NL propose-then-commit.
/// Kick removes the character from the roster; warn/tell are recorded for overlay / chat.
/// </summary>
public sealed class NlKenshiModHost
{
    public static readonly string[] RequiredEvents =
    [
        "sessionStart", "playerJoin", "playerLeave", "playerChat",
        "entityDamage", "buildingDestroy", "steal", "squadOrder",
        "raid", "trade", "itemDestroy", "knockdown",
    ];

    public static readonly string[] RequiredActions = ["warn", "kick", "tell"];

    private readonly INlKenshiDecisionBus _bus;
    private readonly Dictionary<string, OutpostPawn> _pawns = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NlKenshiAction> _applied = [];
    private bool _sessionStarted;

    public NlKenshiModHost(INlKenshiDecisionBus bus) =>
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));

    public IReadOnlyCollection<string> ConnectedPawns => _pawns.Keys;

    public IReadOnlyList<NlKenshiAction> AppliedActions => _applied;

    public bool SessionStarted => _sessionStarted;

    public NlKenshiDecision StartSession()
    {
        var decision = _bus.Evaluate("sessionStart", "system", new Dictionary<string, double> { ["map.id"] = 1 });
        if (decision.Allowed)
        {
            _sessionStarted = true;
        }

        return decision;
    }

    public NlKenshiDecision TryJoin(string player)
    {
        EnsureSession();
        if (_pawns.ContainsKey(player))
        {
            return NlKenshiDecision.Block("already joined", "kick");
        }

        var decision = _bus.Evaluate("playerJoin", player, new Dictionary<string, double> { ["player.alive"] = 1 });
        if (decision.Allowed)
        {
            _pawns[player] = new OutpostPawn(player);
        }

        ApplyIfNeeded(decision, player, "playerJoin");
        return decision;
    }

    public NlKenshiDecision TryChat(string player, string text)
    {
        if (!_pawns.ContainsKey(player))
        {
            return NlKenshiDecision.Block("unknown player");
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

    public NlKenshiDecision TryDamage(string attacker, double damage)
    {
        if (!_pawns.ContainsKey(attacker))
        {
            return NlKenshiDecision.Block("unknown player");
        }

        var decision = _bus.Evaluate("entityDamage", attacker, new Dictionary<string, double>
        {
            ["weapon.damage"] = damage,
            ["player.alive"] = 1,
        });
        ApplyIfNeeded(decision, attacker, "entityDamage");
        return decision;
    }

    public NlKenshiDecision TryBuildingDestroy(string player, double value) =>
        Propose(player, "buildingDestroy", new Dictionary<string, double> { ["building.value"] = value });

    public NlKenshiDecision TrySteal(string player, double value) =>
        Propose(player, "steal", new Dictionary<string, double> { ["steal.value"] = value });

    public NlKenshiDecision TrySquadOrder(string player, bool ordered) =>
        Propose(player, "squadOrder", new Dictionary<string, double> { ["squad.ordered"] = ordered ? 1 : 0 });

    public NlKenshiDecision TryRaid(string player, double severity) =>
        Propose(player, "raid", new Dictionary<string, double> { ["raid.severity"] = severity });

    public NlKenshiDecision TryTrade(string player, double value) =>
        Propose(player, "trade", new Dictionary<string, double> { ["trade.value"] = value });

    public NlKenshiDecision TryItemDestroy(string player, double value) =>
        Propose(player, "itemDestroy", new Dictionary<string, double> { ["item.value"] = value });

    public NlKenshiDecision TryLeave(string player)
    {
        var decision = Propose(player, "playerLeave", new Dictionary<string, double> { ["player.alive"] = 1 });
        _pawns.Remove(player);
        return decision;
    }

    public void Apply(NlKenshiAction action)
    {
        _applied.Add(action);
        if (string.Equals(action.Action, "kick", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action.Action, "despawn", StringComparison.OrdinalIgnoreCase))
        {
            _pawns.Remove(action.Player);
        }
    }

    private NlKenshiDecision Propose(string player, string eventName, Dictionary<string, double> props)
    {
        if (!_pawns.ContainsKey(player) && eventName is not "playerJoin" and not "sessionStart")
        {
            return NlKenshiDecision.Block("unknown player");
        }

        var decision = _bus.Evaluate(eventName, player, props);
        ApplyIfNeeded(decision, player, eventName);
        return decision;
    }

    private void ApplyIfNeeded(NlKenshiDecision decision, string player, string eventName)
    {
        if (decision.Allowed || string.IsNullOrWhiteSpace(decision.Action))
        {
            return;
        }

        Apply(new NlKenshiAction(decision.Action, player, eventName, "Block", decision.Message ?? ""));
    }

    private void EnsureSession()
    {
        if (!_sessionStarted)
        {
            StartSession();
        }
    }

    private sealed record OutpostPawn(string Name);
}

/// <summary>Deterministic in-memory bus for unit tests (no session host required).</summary>
public sealed class NlKenshiRuleBus : INlKenshiDecisionBus
{
    private readonly Func<string, string, IReadOnlyDictionary<string, double>, NlKenshiDecision> _evaluate;

    public NlKenshiRuleBus(Func<string, string, IReadOnlyDictionary<string, double>, NlKenshiDecision> evaluate) =>
        _evaluate = evaluate;

    public NlKenshiDecision Evaluate(string eventName, string player, IReadOnlyDictionary<string, double> props) =>
        _evaluate(eventName, player, props);

    public static NlKenshiRuleBus FromOutpostDefaults() => new((evt, _, props) =>
    {
        if (evt == "playerChat" && props.GetValueOrDefault("chat.capsRatio") > 0.8
            && props.GetValueOrDefault("chat.length") > 10)
        {
            return NlKenshiDecision.Block("please avoid excessive caps in squad chat", "warn");
        }

        if (evt == "entityDamage" && props.GetValueOrDefault("weapon.damage") > 40)
        {
            return NlKenshiDecision.Block("excessive character damage", "kick");
        }

        if (evt == "buildingDestroy" && props.GetValueOrDefault("building.value") > 1500)
        {
            return NlKenshiDecision.Block("high-value outpost destroy blocked", "kick");
        }

        if (evt == "steal" && props.GetValueOrDefault("steal.value") > 800)
        {
            return NlKenshiDecision.Block("high-value steal blocked", "kick");
        }

        if (evt == "raid" && props.GetValueOrDefault("raid.severity") > 8)
        {
            return NlKenshiDecision.Block("mass raid blocked", "kick");
        }

        if (evt == "trade" && props.GetValueOrDefault("trade.value") > 5000)
        {
            return NlKenshiDecision.Block("high-value trade blocked", "warn");
        }

        if (evt == "itemDestroy" && props.GetValueOrDefault("item.value") > 800)
        {
            return NlKenshiDecision.Block("high-value item destroy blocked", "kick");
        }

        return NlKenshiDecision.Allow();
    });
}
