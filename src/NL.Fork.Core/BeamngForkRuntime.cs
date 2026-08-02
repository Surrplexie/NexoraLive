using NL.Core;
using NL.Server.Core;

namespace NL.Fork.Core;

/// <summary>BeamNG.drive fork sidecar — move/crash/airtime/rollover/boundary events (Phase P).</summary>
public sealed class BeamngForkRuntime : IForkRuntimeDetails
{
    private readonly IForkDecisionSink _decisions;
    private readonly ForkModManifest _mods;
    private readonly ForkActionApplicator _applicator = new();
    private readonly Func<string, Task<bool>>? _admitAsync;
    private bool _sessionStarted;

    public BeamngForkRuntime(
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

    public Task<ForkActionResult> TryShootAsync(
        string shooter,
        string target,
        double damage,
        CancellationToken cancellationToken = default) =>
        TryCrashAsync(shooter, damage, cancellationToken);

    public async Task<ForkActionResult> TryMoveAsync(
        string player,
        double speedMph,
        double y,
        double z,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        state.X = speedMph;
        var moveEvent = ForkEventFactory.BeamngMove(state, speedMph, _mods);
        var outcome = await _decisions.EvaluateAsync(moveEvent, cancellationToken);

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

    public async Task<ForkActionResult> TryRespawnAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var respawnEvent = ForkEventFactory.Respawn(state, state.Health, _mods);
        var outcome = await _decisions.EvaluateAsync(respawnEvent, cancellationToken);
        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public Task<ForkActionResult> TryChatAsync(
        string player,
        string text,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ForkActionResult(true, Decision.Allow, null));

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

    public async Task<ForkActionResult> TryCrashAsync(
        string player,
        double severity,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var crashEvent = ForkEventFactory.Crash(state, severity, _mods);
        var outcome = await _decisions.EvaluateAsync(crashEvent, cancellationToken);
        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryAirtimeAsync(
        string player,
        double seconds,
        CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var airtimeEvent = ForkEventFactory.Airtime(state, seconds, _mods);
        var outcome = await _decisions.EvaluateAsync(airtimeEvent, cancellationToken);
        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryRolloverAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var rolloverEvent = ForkEventFactory.Rollover(state, _mods);
        var outcome = await _decisions.EvaluateAsync(rolloverEvent, cancellationToken);
        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task<ForkActionResult> TryLeaveBoundaryAsync(string player, CancellationToken cancellationToken = default)
    {
        if (!World.TryGetPlayer(player, out var state) || state is null)
        {
            return new ForkActionResult(false, Decision.Block, "unknown player");
        }

        var boundaryEvent = ForkEventFactory.LeaveBoundary(state, _mods);
        var outcome = await _decisions.EvaluateAsync(boundaryEvent, cancellationToken);
        if (outcome.Decision == Decision.Block && outcome.Action is not null)
        {
            _applicator.Apply(outcome.Action, World);
            return new ForkActionResult(false, outcome.Decision, outcome.Message, outcome.Action?.Action);
        }

        return new ForkActionResult(true, Decision.Allow, outcome.Message);
    }

    public async Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        await _decisions.EvaluateAsync(ForkEventFactory.SessionEnd(_mods), cancellationToken);
    }
}
