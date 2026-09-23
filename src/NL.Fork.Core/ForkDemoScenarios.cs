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
            ForkGameKind.RimWorld => RunRimWorldOnceAsync(runtime, log, cancellationToken),
            ForkGameKind.Kenshi => RunKenshiOnceAsync(runtime, log, cancellationToken),
            _ => RunHelloOnceAsync(runtime, log, cancellationToken),
        };

    /// <summary>Emit a single sessionStart and hold — no fake players / sessionEnd.</summary>
    public static Task EnsureSessionStartedAsync(
        ForkGameKind game,
        IForkRuntimeDetails runtime,
        CancellationToken cancellationToken) =>
        game switch
        {
            ForkGameKind.Minecraft when runtime is MinecraftForkRuntime mc =>
                mc.EnsureSessionStartedAsync(cancellationToken),
            ForkGameKind.Beamng when runtime is BeamngForkRuntime beamng =>
                beamng.EnsureSessionStartedAsync(cancellationToken),
            ForkGameKind.RimWorld when runtime is RimWorldForkRuntime rim =>
                rim.EnsureSessionStartedAsync(cancellationToken),
            ForkGameKind.Kenshi when runtime is KenshiForkRuntime ken =>
                ken.EnsureSessionStartedAsync(cancellationToken),
            _ when runtime is HelloForkRuntime hello =>
                hello.EnsureSessionStartedAsync(cancellationToken),
            _ => Task.CompletedTask,
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

    private static async Task RunRimWorldOnceAsync(
        IForkRuntimeDetails runtime,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (runtime is RimWorldForkRuntime rim)
        {
            await rim.EnsureSessionStartedAsync(cancellationToken);
        }

        foreach (var player in new[] { "Randy", "Cassandra" })
        {
            var join = await runtime.TryJoinAsync(player, cancellationToken);
            log?.Invoke($"[rimworld] join {player} → {(join.Committed ? "ok" : join.Message)}");
        }

        var chat = await runtime.TryChatAsync("Randy", "HELLO COLONY!!!", cancellationToken);
        log?.Invoke($"[rimworld] caps chat → committed={chat.Committed} decision={chat.Decision}");

        var damage = await runtime.TryShootAsync("Randy", "Cassandra", 8, cancellationToken);
        log?.Invoke($"[rimworld] entityDamage → committed={damage.Committed}");

        if (runtime is RimWorldForkRuntime colony)
        {
            var grief = await colony.TryBuildingDestroyAsync("Randy", 2500, cancellationToken);
            log?.Invoke($"[rimworld] buildingDestroy value=2500 → committed={grief.Committed} decision={grief.Decision}");

            var zone = await colony.TryZoneEditAsync("Cassandra", 40, cancellationToken);
            log?.Invoke($"[rimworld] zoneEdit tiles=40 → committed={zone.Committed}");

            var animals = await colony.TryAnimalReleaseAsync("Randy", 6, cancellationToken);
            log?.Invoke($"[rimworld] animalRelease count=6 → committed={animals.Committed} decision={animals.Decision}");

            var draft = await colony.TryDraftAsync("Cassandra", true, cancellationToken);
            log?.Invoke($"[rimworld] colonistDraft → committed={draft.Committed}");

            var trade = await colony.TryTradeAsync("Randy", 9000, cancellationToken);
            log?.Invoke($"[rimworld] trade value=9000 → committed={trade.Committed} decision={trade.Decision}");

            var item = await colony.TryItemDestroyAsync("Randy", 50, cancellationToken);
            log?.Invoke($"[rimworld] itemDestroy → committed={item.Committed}");

            var move = await colony.TryMoveAsync("Cassandra", 8, 0, 0, cancellationToken);
            log?.Invoke($"[rimworld] move → committed={move.Committed}");

            await colony.EndSessionAsync(cancellationToken);
        }

        foreach (var player in new[] { "Randy", "Cassandra" })
        {
            if (runtime.World.TryGetPlayer(player, out _))
            {
                await runtime.TryLeaveAsync(player, cancellationToken);
            }
        }
    }

    private static async Task RunKenshiOnceAsync(
        IForkRuntimeDetails runtime,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (runtime is KenshiForkRuntime ken)
        {
            await ken.EnsureSessionStartedAsync(cancellationToken);
        }

        foreach (var player in new[] { "Beep", "Shinobi" })
        {
            var join = await runtime.TryJoinAsync(player, cancellationToken);
            log?.Invoke($"[kenshi] join {player} → {(join.Committed ? "ok" : join.Message)}");
        }

        var chat = await runtime.TryChatAsync("Beep", "HELLO SQUAD!!!", cancellationToken);
        log?.Invoke($"[kenshi] caps chat → committed={chat.Committed} decision={chat.Decision}");

        var damage = await runtime.TryShootAsync("Beep", "Shinobi", 8, cancellationToken);
        log?.Invoke($"[kenshi] entityDamage → committed={damage.Committed}");

        if (runtime is KenshiForkRuntime world)
        {
            var steal = await world.TryStealAsync("Beep", 1200, cancellationToken);
            log?.Invoke($"[kenshi] steal value=1200 → committed={steal.Committed} decision={steal.Decision}");

            var grief = await world.TryBuildingDestroyAsync("Beep", 2500, cancellationToken);
            log?.Invoke($"[kenshi] buildingDestroy value=2500 → committed={grief.Committed} decision={grief.Decision}");

            var raid = await world.TryRaidAsync("Beep", 12, cancellationToken);
            log?.Invoke($"[kenshi] raid severity=12 → committed={raid.Committed} decision={raid.Decision}");

            var squad = await world.TrySquadOrderAsync("Shinobi", true, cancellationToken);
            log?.Invoke($"[kenshi] squadOrder → committed={squad.Committed}");

            var trade = await world.TryTradeAsync("Beep", 9000, cancellationToken);
            log?.Invoke($"[kenshi] trade value=9000 → committed={trade.Committed} decision={trade.Decision}");

            var item = await world.TryItemDestroyAsync("Beep", 50, cancellationToken);
            log?.Invoke($"[kenshi] itemDestroy → committed={item.Committed}");

            var move = await world.TryMoveAsync("Shinobi", 8, 0, 0, cancellationToken);
            log?.Invoke($"[kenshi] move → committed={move.Committed}");

            await world.EndSessionAsync(cancellationToken);
        }

        foreach (var player in new[] { "Beep", "Shinobi" })
        {
            if (runtime.World.TryGetPlayer(player, out _))
            {
                await runtime.TryLeaveAsync(player, cancellationToken);
            }
        }
    }
}
