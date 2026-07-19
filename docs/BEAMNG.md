# BeamNG.drive + NexoraLive (NL)

BeamNG has no Minecraft-style server log + RCON. NL integrates through the existing
**generic NDJSON** path: a Lua bridge mod emits sparse session events; Session Host /
`NL.Server --game generic` evaluates them; Blocks go back over localhost UDP.

## Architecture

```
BeamNG (NL_BeamNGBridge Lua)
  → %LOCALAPPDATA%\NL\beamng-events.ndjson
  → NL.SessionHost (game=generic, anti-cheat on, join gate off for solo)
  → RuleEngine (+ optional anomaly*)
  → UDP 127.0.0.1:27022  (SCBN1 warn|recover|kick)
  → BeamNG toast / recover
```

## Install bridge

```powershell
powershell -File scripts/install-beamng-bridge.ps1
```

Or copy [`beamng-mod/NL_BeamNGBridge`](../beamng-mod/NL_BeamNGBridge) to
`<BeamNG user folder>/mods/unpacked/NL_BeamNGBridge`, enable the mod, load a map.

Events append to `%LOCALAPPDATA%\NL\beamng-events.ndjson`.

## Session Host (solo freeroam)

1. `dotnet run --project src/NL.SessionHost`
2. **Tools → Load BeamNG freeroam defaults**
   - Game: `generic`
   - Config: `samples/configs/beamng.nle`
   - Source: `%LOCALAPPDATA%\NL\beamng-events.ndjson`
   - BeamNG UDP: `127.0.0.1:27022`
   - Anti-cheat: **on**
   - Join gate: **off** (solo — enable under BeamMP)
3. Start session, then drive in BeamNG.

Example profile: [`samples/session-profile.beamng.example.json`](../samples/session-profile.beamng.example.json).

## CLI replay (no game)

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/beamng.nle \
  --source samples/events/beamng-sample.ndjson \
  --replay --anti-cheat
```

Live with in-game actions:

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/beamng.nle \
  --source %LOCALAPPDATA%\NL\beamng-events.ndjson \
  --anti-cheat --beamng-cmd 127.0.0.1:27022
```

## Event vocabulary (bridge)

| Event | Meaning |
|---|---|
| `sessionStart` / `sessionEnd` | Map load / leave |
| `playerJoin` / `playerLeave` | Local driver (and BeamMP remote when hooks fire) |
| `move` | Throttled pose + `vehicle.speed` |
| `crash` | Sudden speed loss proxy |
| `airtime` / `rollover` | Air / inverted timers |
| `leaveBoundary` | Outside default AABB (edit `BOUNDARY` in Lua) |
| `recover` / `respawn` | Recover / reset |

## UDP command protocol

UTF-8 datagram:

```
SCBN1
{action}|{player}|{message}
```

| Action | NL uses when | Bridge effect |
|---|---|---|
| `warn` | overspeed, soft blocks | in-game toast |
| `recover` | crash / rollover / leaveBoundary / airtime / anomaly* | toast + recover vehicle |
| `kick` / `despawn` | join-gate Block on `playerJoin` | toast + recover (BeamMP: replace with real kick) |

## BeamMP (multiplayer layer)

1. Run the same bridge on the **host** PC with NL Session Host.
2. Turn **Join gate** on once `playerJoin` lines arrive with real BeamMP names (`beammp=1` prop).
3. Ban in Moderation Console (streamer id must match) → next `playerJoin` → UDP `kick`.
4. Lua hooks `onPlayerConnected` / `onPlayerDisconnected` emit join/leave when the MP stack
   calls them; if your BeamMP build uses different hook names, forward into
   `extensions.NL_bridge` (see `bridge.lua`).

Until those hooks fire, treat multiplayer as not ready for join gate.

## Tuning (dogfood)

Edit thresholds at the top of
`beamng-mod/NL_BeamNGBridge/lua/ge/extensions/NL/bridge.lua`:

- `MOVE_INTERVAL`, `CRASH_DV_THRESHOLD`, `AIRTIME_THRESHOLD`, `ROLLOVER_THRESHOLD`, `BOUNDARY`

Log false positives in [DOGFOOD_BEAMNG.md](DOGFOOD_BEAMNG.md).

## API caveats

BeamNG Lua APIs differ by version. The bridge uses `pcall` around vehicle pose / recover /
toasts. If events stop or recover fails after a game update, adjust the GE extension against
that build’s docs (`be:getPlayerVehicle`, `guihooks`, etc.). OutGauge/MotionSim are **not**
required for MVP.
