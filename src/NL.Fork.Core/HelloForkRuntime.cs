using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>Reference <see cref="IForkRuntime"/> — minimal FPS-like fork for Phase P smoke tests.</summary>
public sealed class HelloForkRuntime : IForkRuntime
{
    private readonly IForkDecisionSink _decisions;
    private readonly ForkModManifest _mods;
    private readonly ForkStateValidator _validator = new();
    private readonly ForkActionApplicator _applicator = new();
    private readonly Func<string, Task<bool>>? _admitAsync;
    private bool _sessionStarted;

    public HelloForkRuntime(
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

        var evt = ForkEventFactory.SessionStart(_mods);
        await _decisions.EvaluateAsync(evt, cancellationToken);
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

        var shootEvent = ForkEventFactory.Shoot(shooterState, targetState, damage, _mods);
        var outcome = await _decisions.EvaluateAsync(shootEvent, cancellationToken);

        if (outcome.Decision == Decision.Block)
        {
            if (outcome.Action is not null)
            {
                _applicator.Apply(outcome.Action, World);
            }
            else
            {
                _applicator.ApplyBlockSideEffects(shootEvent, ActionResult.Block(outcome.Message), World);
            }

            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        if (targetState is not null)
        {
            targetState.Health = Math.Max(0, targetState.Health - damage);
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
            var boundaryEvent = ForkEventFactory.LeaveBoundary(state, _mods);
            var boundaryOutcome = await _decisions.EvaluateAsync(boundaryEvent, cancellationToken);
            if (boundaryOutcome.Action is not null)
            {
                _applicator.Apply(boundaryOutcome.Action, World);
            }

            return new ForkActionResult(false, Decision.Block, validation.Reason);
        }

        state.X = x;
        state.Y = y;
        state.Z = z;

        var moveEvent = ForkEventFactory.Move(state, _mods);
        var outcome = await _decisions.EvaluateAsync(moveEvent, cancellationToken);
        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryRespawnAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var requestedHealth = state.Alive ? 0 : 100;
        var validation = _validator.ValidateRespawn(state, requestedHealth);
        if (!validation.Allowed)
        {
            var respawnPreview = ForkEventFactory.Respawn(state, state.Health, _mods);
            var outcome = await _decisions.EvaluateAsync(respawnPreview, cancellationToken);
            if (outcome.Decision == Decision.Block)
            {
                if (outcome.Action is not null)
                {
                    _applicator.Apply(outcome.Action, World);
                }

                return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
            }
        }

        var health = state.Alive ? state.Health : 100;
        if (!state.Alive)
        {
            state.Health = 100;
        }

        return new ForkActionResult(true, Decision.Allow, null);
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
}
