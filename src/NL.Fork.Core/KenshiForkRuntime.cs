using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>
/// Full Kenshi fork runtime (Phase T1) — community-MP vocabulary for NL-hosted sidecar
/// hosts. Propose-then-commit for chat, combat, steal, squad orders, raids, trade, and KO.
/// Kenshi has no official dedicated server; operators mount a licensed install for in-game mods.
/// </summary>
public sealed class KenshiForkRuntime : IForkRuntimeDetails
{
    private readonly IForkDecisionSink _decisions;
    private readonly ForkModManifest _mods;
    private readonly ForkStateValidator _validator = new() { MaxMovePerTick = 500 };
    private readonly ForkActionApplicator _applicator = new();
    private readonly Func<string, Task<bool>>? _admitAsync;
    private bool _sessionStarted;

    public KenshiForkRuntime(
        IForkDecisionSink decisions,
        ForkModManifest? mods = null,
        Func<string, Task<bool>>? admitAsync = null)
    {
        _decisions = decisions;
        _mods = mods ?? new ForkModManifest();
        _admitAsync = admitAsync;
        World = new ForkWorldState { BoundaryMin = -1000, BoundaryMax = 1000 };
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
        var outcome = await _decisions.EvaluateAsync(ForkEventFactory.PlayerJoin(preview, _mods), cancellationToken);
        if (IsBlocked(outcome, out var blocked))
        {
            return blocked;
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

        var outcome = await _decisions.EvaluateAsync(
            ForkEventFactory.EntityDamage(shooterState, targetState, damage, _mods),
            cancellationToken);
        if (IsBlocked(outcome, out var blocked))
        {
            return blocked;
        }

        if (targetState is not null)
        {
            targetState.Health = Math.Max(0, targetState.Health - damage);
            if (targetState.Health <= 0)
            {
                targetState.Health = 0;
                targetState.Downed = true;
                targetState.Drafted = false;
                await TryKnockdownAsync(target, cancellationToken);
            }
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryMoveAsync(
        string player,
        double x,
        double y,
        double z,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var validation = _validator.ValidateMove(state, x, y, z, World);
        if (!validation.Allowed)
        {
            return new ForkActionResult(false, Decision.Block, validation.Reason);
        }

        var preview = state.Clone();
        preview.X = x;
        preview.Y = y;
        preview.Z = z;
        var outcome = await _decisions.EvaluateAsync(ForkEventFactory.Move(preview, _mods), cancellationToken);
        if (IsBlocked(outcome, out var blocked))
        {
            return blocked;
        }

        state.X = x;
        state.Y = y;
        state.Z = z;
        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryRespawnAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var validation = _validator.ValidateRespawn(state, 100);
        if (!validation.Allowed && !state.Downed)
        {
            return new ForkActionResult(false, Decision.Block, validation.Reason);
        }

        var outcome = await _decisions.EvaluateAsync(ForkEventFactory.Respawn(state, 100, _mods), cancellationToken);
        if (IsBlocked(outcome, out var blocked))
        {
            return blocked;
        }

        state.Health = 100;
        state.Downed = false;
        state.Drafted = false;
        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public Task<ForkActionResult> TryChatAsync(
        string player,
        string text,
        CancellationToken cancellationToken = default) =>
        ProposePlayerAsync(
            player,
            state => ForkEventFactory.PlayerChat(state, text, _mods),
            cancellationToken: cancellationToken);

    public async Task<ForkActionResult> TryLeaveAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        await _decisions.EvaluateAsync(ForkEventFactory.PlayerLeave(state, _mods), cancellationToken);
        World.RemovePlayer(player);
        return new ForkActionResult(true, Decision.Allow, null);
    }

    public Task<ForkActionResult> TryBuildingDestroyAsync(
        string player,
        double value,
        CancellationToken cancellationToken = default) =>
        ProposePlayerAsync(
            player,
            state => ForkEventFactory.BuildingDestroy(state, value, _mods),
            cancellationToken: cancellationToken);

    public Task<ForkActionResult> TryStealAsync(
        string player,
        double value,
        CancellationToken cancellationToken = default) =>
        ProposePlayerAsync(
            player,
            state => ForkEventFactory.Steal(state, value, _mods),
            cancellationToken: cancellationToken);

    public Task<ForkActionResult> TrySquadOrderAsync(
        string player,
        bool ordered,
        CancellationToken cancellationToken = default) =>
        ProposePlayerAsync(
            player,
            state => ForkEventFactory.SquadOrder(state, ordered, _mods),
            onCommit: state => state.Drafted = ordered,
            cancellationToken: cancellationToken);

    public Task<ForkActionResult> TryRaidAsync(
        string player,
        double severity,
        CancellationToken cancellationToken = default) =>
        ProposePlayerAsync(
            player,
            state => ForkEventFactory.Raid(state, severity, _mods),
            cancellationToken: cancellationToken);

    public Task<ForkActionResult> TryTradeAsync(
        string player,
        double value,
        CancellationToken cancellationToken = default) =>
        ProposePlayerAsync(
            player,
            state => ForkEventFactory.Trade(state, value, _mods),
            cancellationToken: cancellationToken);

    public Task<ForkActionResult> TryItemDestroyAsync(
        string player,
        double value,
        CancellationToken cancellationToken = default) =>
        ProposePlayerAsync(
            player,
            state => ForkEventFactory.ItemDestroy(state, value, _mods),
            cancellationToken: cancellationToken);

    public async Task<ForkActionResult> TryKnockdownAsync(
        string player,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        state.Downed = true;
        state.Drafted = false;
        if (state.Health > 0)
        {
            state.Health = 0;
        }

        var outcome = await _decisions.EvaluateAsync(ForkEventFactory.Knockdown(state, _mods), cancellationToken);
        if (IsBlocked(outcome, out var blocked))
        {
            return blocked;
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        await _decisions.EvaluateAsync(ForkEventFactory.SessionEnd(_mods), cancellationToken);
        _sessionStarted = false;
    }

    private async Task<ForkActionResult> ProposePlayerAsync(
        string player,
        Func<ForkPlayerState, SessionEvent> makeEvent,
        Action<ForkPlayerState>? onCommit = null,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var outcome = await _decisions.EvaluateAsync(makeEvent(state), cancellationToken);
        if (IsBlocked(outcome, out var blocked))
        {
            return blocked;
        }

        onCommit?.Invoke(state);
        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    private bool IsBlocked(ForkDecisionOutcome outcome, out ForkActionResult blocked)
    {
        if (outcome.Decision != Decision.Block)
        {
            blocked = default!;
            return false;
        }

        if (outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
        }

        blocked = new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        return true;
    }
}
