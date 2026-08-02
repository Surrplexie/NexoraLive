namespace NL.Fork.Core;

/// <summary>Per-game demo loops for NL.Fork.Runtime container smoke tests.</summary>
public static class ForkDemoScenarios
{
    public static async Task RunLoopAsync(
        ForkGameKind game,
        IForkRuntimeDetails runtime,
        double intervalSeconds,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RunOnceAsync(game, runtime, log, cancellationToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public static Task RunOnceAsync(
        ForkGameKind game,
        IForkRuntimeDetails runtime,
        Action<string>? log,
        CancellationToken cancellationToken) =>
        game switch
        {
            ForkGameKind.Minecraft => RunMinecraftOnceAsync(runtime, log, cancellationToken),
            ForkGameKind.Beamng => RunBeamngOnceAsync(runtime, log, cancellationToken),
            _ => RunHelloOnceAsync(runtime, log, cancellationToken),
        };

    private static async Task RunHelloOnceAsync(
        IForkRuntimeDetails runtime,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var players = new[] { "Alice", "Bob" };
        foreach (var player in players)
        {
            var join = await runtime.TryJoinAsync(player, cancellationToken);
            log?.Invoke($"[fork] join {player} → {(join.Committed ? "ok" : join.Message)}");
        }

        var shoot = await runtime.TryShootAsync("Alice", "Bob", 12, cancellationToken);
        log?.Invoke($"[fork] Alice shoots Bob → committed={shoot.Committed} decision={shoot.Decision}");

        if (runtime.World.TryGetPlayer("Bob", out var bob) && bob is not null)
        {
            log?.Invoke($"[fork] Bob health={bob.Health}");
        }

        await runtime.TryChatAsync("Bob", "HELLO EVERYONE!!!", cancellationToken);
        await runtime.TryRespawnAsync("Bob", cancellationToken);

        foreach (var player in players)
        {
            if (runtime.World.TryGetPlayer(player, out _))
            {
                await runtime.TryLeaveAsync(player, cancellationToken);
            }
        }
    }

    private static async Task RunMinecraftOnceAsync(
        IForkRuntimeDetails runtime,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (runtime is MinecraftForkRuntime mc)
        {
            await mc.EnsureSessionStartedAsync(cancellationToken);
        }

        foreach (var player in new[] { "Steve", "Alex" })
        {
            var join = await runtime.TryJoinAsync(player, cancellationToken);
            log?.Invoke($"[minecraft] join {player} → {(join.Committed ? "ok" : join.Message)}");
        }

        var chat = await runtime.TryChatAsync("Steve", "HELLO EVERYONE!!!", cancellationToken);
        log?.Invoke($"[minecraft] caps chat → committed={chat.Committed} decision={chat.Decision}");

        var damage = await runtime.TryShootAsync("Steve", "Alex", 10, cancellationToken);
        log?.Invoke($"[minecraft] entityDamage → committed={damage.Committed}");

        if (runtime is MinecraftForkRuntime minecraft)
        {
            await minecraft.TryAdvancementAsync("Steve", "minecraft:story/root", cancellationToken);
        }

        foreach (var player in new[] { "Steve", "Alex" })
        {
            if (runtime.World.TryGetPlayer(player, out _))
            {
                await runtime.TryLeaveAsync(player, cancellationToken);
            }
        }
    }

    private static async Task RunBeamngOnceAsync(
        IForkRuntimeDetails runtime,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (runtime is BeamngForkRuntime beamng)
        {
            await beamng.EnsureSessionStartedAsync(cancellationToken);
            var join = await runtime.TryJoinAsync("Driver1", cancellationToken);
            log?.Invoke($"[beamng] join Driver1 → {(join.Committed ? "ok" : join.Message)}");

            var slow = await runtime.TryMoveAsync("Driver1", 40, 0, 0, cancellationToken);
            log?.Invoke($"[beamng] move 40mph → committed={slow.Committed}");

            var fast = await runtime.TryMoveAsync("Driver1", 62, 0, 0, cancellationToken);
            log?.Invoke($"[beamng] move 62mph → committed={fast.Committed} decision={fast.Decision}");

            await beamng.TryAirtimeAsync("Driver1", 2.0, cancellationToken);
            var crash = await beamng.TryCrashAsync("Driver1", 14, cancellationToken);
            log?.Invoke($"[beamng] crash severity=14 → committed={crash.Committed} decision={crash.Decision}");

            await beamng.TryRolloverAsync("Driver1", cancellationToken);
            await beamng.TryLeaveBoundaryAsync("Driver1", cancellationToken);
            await runtime.TryLeaveAsync("Driver1", cancellationToken);
            await beamng.EndSessionAsync(cancellationToken);
        }
    }
}
