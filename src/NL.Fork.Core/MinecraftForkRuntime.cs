using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>Minecraft Java fork runtime — playerJoin, playerChat, entityDamage, playerDeath (Phase P).</summary>
public sealed class MinecraftForkRuntime : IForkRuntimeDetails
{
    private readonly IForkDecisionSink _decisions;
    private readonly ForkModManifest _mods;
    private readonly ForkStateValidator _validator = new();
    private readonly ForkActionApplicator _applicator = new();
    private readonly Func<string, Task<bool>>? _admitAsync;
    private readonly Dictionary<string, int> _deathCounts = new(StringComparer.OrdinalIgnoreCase);
    private bool _sessionStarted;

    public MinecraftForkRuntime(
        IForkDecisionSink decisions,
        ForkModManifest? mods = null,
        Func<string, Task<bool>>? admitAsync = null)
    {
        _decisions = decisions;
        _mods = mods ?? new ForkModManifest();
        _admitAsync = admitAsync;
        World = new ForkWorldState();
    }

    public ForkWorldState World { get; }

    public ForkModManifest Mods => _mods;

    public IReadOnlyList<ForkAppliedAction> AppliedActions => _applicator.Applied;

    public async Task EnsureSessionStartedAsync(CancellationToken cancellationToken)
    {
        if (_sessionStarted)
        {
            return;
        }

        await _decisions.EvaluateAsync(ForkEventFactory.SessionStart(_mods), cancellationToken);
        _sessionStarted = true;
    }

    public async Task<ForkActionResult> TryJoinAsync(string playerName, CancellationToken cancellationToken = default)
    {
        await EnsureSessionStartedAsync(cancellationToken);

        if (_admitAsync is not null && !await _admitAsync(playerName))
        {
            return new ForkActionResult(false, Decision.Block, "admit denied");
        }

        if (World.TryGetPlayer(playerName, out _))
        {
            return new ForkActionResult(false, Decision.Block, "already joined");
        }

        var preview = new ForkPlayerState { Name = playerName };
        var joinEvent = ForkEventFactory.PlayerJoin(preview, _mods);
        var outcome = await _decisions.EvaluateAsync(joinEvent, cancellationToken);

        if (outcome.Decision == Decision.Block)
        {
            if (outcome.Action is not null)
            {
                _applicator.Apply(outcome.Action, World);
            }

            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        World.AddPlayer(playerName);
        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryShootAsync(
        string shooter,
        string target,
        double damage,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(shooter, out var shooterState) || shooterState is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown shooter");
        }

        World.TryGetPlayer(target, out var targetState);

        var validation = _validator.ValidateShoot(shooterState, targetState, damage);
        if (!validation.Allowed)
        {
            return new ForkActionResult(false, Decision.Block, validation.Reason);
        }

        var damageEvent = ForkEventFactory.EntityDamage(shooterState, targetState, damage, _mods);
        var outcome = await _decisions.EvaluateAsync(damageEvent, cancellationToken);

        if (outcome.Decision == Decision.Block)
        {
            if (outcome.Action is not null)
            {
                _applicator.Apply(outcome.Action, World);
            }

            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        if (targetState is not null)
        {
            targetState.Health = Math.Max(0, targetState.Health - damage);
            if (targetState.Health <= 0)
            {
                targetState.Health = 0;
                await TryDeathAsync(target, cancellationToken);
            }
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public Task<ForkActionResult> TryMoveAsync(
        string player,
        double x,
        double y,
        double z,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ForkActionResult(true, Decision.Allow, null));

    public async Task<ForkActionResult> TryRespawnAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var respawnEvent = ForkEventFactory.Respawn(state, 20, _mods);
        var outcome = await _decisions.EvaluateAsync(respawnEvent, cancellationToken);
        if (outcome.Decision == Decision.Block)
        {
            if (outcome.Action is not null)
            {
                _applicator.Apply(outcome.Action, World);
            }

            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        state.Health = 20;
        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryChatAsync(
        string player,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var chatEvent = ForkEventFactory.PlayerChat(state, text, _mods);
        var outcome = await _decisions.EvaluateAsync(chatEvent, cancellationToken);
        if (outcome.Decision == Decision.Block)
        {
            if (outcome.Action is not null)
            {
                _applicator.Apply(outcome.Action, World);
            }

            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryLeaveAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var leaveEvent = ForkEventFactory.PlayerLeave(state, _mods);
        await _decisions.EvaluateAsync(leaveEvent, cancellationToken);
        World.RemovePlayer(player);
        return new ForkActionResult(true, Decision.Allow, null);
    }

    public async Task<ForkActionResult> TryDeathAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        _deathCounts.TryGetValue(player, out var count);
        count++;
        _deathCounts[player] = count;

        var deathEvent = ForkEventFactory.PlayerDeath(state, _mods, count);
        var outcome = await _decisions.EvaluateAsync(deathEvent, cancellationToken);
        state.Health = 0;

        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryAdvancementAsync(
        string player,
        string advancementId,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var advEvent = ForkEventFactory.PlayerAdvancement(state, advancementId, _mods);
        var outcome = await _decisions.EvaluateAsync(advEvent, cancellationToken);
        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }
}
