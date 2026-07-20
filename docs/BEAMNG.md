# BeamNG.drive + NexoraLive (NL)

BeamNG has no Minecraft-style server log + RCON. NL integrates through the existing
**generic NDJSON** path: a Lua bridge mod emits sparse session events; Session Host /
`NL.Server --game generic` evaluates them; Blocks go back over localhost UDP.

## Known-good operator profile (0.2 bridge)

| Item | Value |
|---|---|
| Bridge mod | `NL_BeamNGBridge` **0.2.0** (`beamng-mod/NL_BeamNGBridge`) |
| Emit floors (`bridge.json` / Lua defaults) | crash Δv **10**, airtime **1.5s**, rollover **1.75s**, move **0.35s** |
| `.nle` Block gates (`samples/configs/beamng.nle`) | speed **> 55**, crash **> 12**, airtime **> 1.5**, anomaly distance **> 120** |
| Anti-cheat with `--beamng-cmd` | `AnomalyThresholds.BeamNgFreeroam` (teleport ≤150 / ≤90 u/s) |
| BeamMP kick | Queue file + `NL_Kick` server plugin → `MP.DropPlayer` |
| Live dogfood on this machine | Fill after install — see [DOGFOOD_BEAMNG.md](DOGFOOD_BEAMNG.md) |
| Known Steam + user data (this workstation) | Game **0.38.6** at Steam `...\common\BeamNG.drive`; user data `%LOCALAPPDATA%\BeamNG\BeamNG.drive\current` |

Steam **game** folder ≠ **user** folder. Mods install under user data (`...\current\mods\unpacked\`).
Override discovery with `BEAMNG_USER_FOLDER` if needed.

## Architecture

```
BeamNG (NL_BeamNGBridge Lua)
  → %LOCALAPPDATA%\NL\beamng-events.ndjson
  → NL.SessionHost (game=generic, anti-cheat on, join gate off for solo)
  → RuleEngine (+ BeamNgFreeroam anomaly* when --beamng-cmd set)
  → UDP 127.0.0.1:27022  (SCBN1 warn|recover|kick)
  → BeamNG toast / recover
  → (kick) %LOCALAPPDATA%\NL\beamng-kicks.ndjson → BeamMP NL_Kick → MP.DropPlayer
```

## Install bridge

```powershell
powershell -File scripts/install-beamng-bridge.ps1
```

Or copy [`beamng-mod/NL_BeamNGBridge`](../beamng-mod/NL_BeamNGBridge) to
`<BeamNG user folder>/mods/unpacked/NL_BeamNGBridge`, enable the mod, load a map.

The mod **must** include `scripts/NL_BeamNGBridge/modScript.lua` (calls `load("NL_bridge")`).
Without it BeamNG shows the mod under **unpacked** but the GE extension never runs — no NDJSON,
no UDP, **Decisions: 0** in Session Host.

Optional threshold / port overrides: edit `bridge.json` in the mod folder (copied with install).

Events append to `%LOCALAPPDATA%\NL\beamng-events.ndjson`.

## Session Host (solo freeroam)

1. `dotnet run --project src/NL.SessionHost`
2. **Tools → Load BeamNG freeroam defaults**
   - Game: `generic`
   - Config: `samples/configs/beamng.nle`
   - Source: `%LOCALAPPDATA%\NL\beamng-events.ndjson`
   - BeamNG UDP: `127.0.0.1:27022`
   - Anti-cheat: **on** (uses freeroam thresholds because UDP endpoint is set)
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

Live with in-game actions (also selects BeamNG freeroam anomaly thresholds):

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
| `leaveBoundary` | Outside AABB (edit `BOUNDARY` / `bridge.json`) |
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
| `recover` | crash / rollover / leaveBoundary / airtime / anomaly* | toast + multi-API recover |
| `kick` / `despawn` | join-gate Block on `playerJoin` | BeamMP kick queue (+ DropPlayer if present); else toast + recover |

If UDP bind fails (port busy), the bridge toasts and logs — change `cmdPort` in `bridge.json`
and the Session Host **BeamNG UDP** field to match.

## BeamMP (multiplayer layer)

See [`beamng-mod/NL_BeamNGBridge/BEAMMP.md`](../beamng-mod/NL_BeamNGBridge/BEAMMP.md).

```powershell
powershell -File scripts/install-beammp-nl-kick.ps1 -ServerRoot "D:\path\to\BeamMP-Server"
```

## Tuning (dogfood)

Edit thresholds in:

1. [`bridge.json`](../beamng-mod/NL_BeamNGBridge/bridge.json) (preferred) or the top of `bridge.lua`
2. [`samples/configs/beamng.nle`](../samples/configs/beamng.nle) Block conditions (keep ≥ emit floors)

Log false positives in [DOGFOOD_BEAMNG.md](DOGFOOD_BEAMNG.md).

## API caveats

BeamNG Lua APIs differ by version. The bridge uses `pcall` around vehicle pose / recover /
toasts and tries several recover entry points. If events stop or recover fails after a game
update, adjust the GE extension against that build’s docs. OutGauge/MotionSim are **not**
required for MVP.
