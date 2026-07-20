# BeamMP notes for NL_BeamNGBridge

Solo freeroam works without BeamMP. Multiplayer join gate needs player identity events
and a real kick path.

## Host setup

1. Host runs BeamNG + BeamMP + `NL_BeamNGBridge` + NL Session Host (join gate **on**).
2. Install the companion server plugin:
   ```powershell
   powershell -File scripts/install-beammp-nl-kick.ps1 -ServerRoot "D:\path\to\BeamMP-Server"
   ```
   Or copy [`NL_BeamMPServer/NL_Kick`](../NL_BeamMPServer/NL_Kick) to
   `<BeamMP Server>/Resources/Server/NL_Kick`.
3. Confirm `playerJoin` lines appear in `%LOCALAPPDATA%\NL\beamng-events.ndjson` for remote
   names (prop `beammp: 1`).
4. Ban a name in Moderation Console → that player's next join should UDP `kick` → bridge
   queues `%LOCALAPPDATA%\NL\beamng-kicks.ndjson` → `NL_Kick` calls `MP.DropPlayer`.

If the BeamMP server runs as a different Windows user, set env `NL_BEAMNG_KICKS` on the
server process to the same queue path the bridge writes.

## Hook wiring

`lua/ge/extensions/NL/bridge.lua` exposes:

- `onPlayerConnected(playerId, name)` — stores name→id and emits `playerJoin`
- `onPlayerDisconnected(playerId, name)` — emits `playerLeave`

If your BeamMP plugin uses different callback names, call into the loaded NL extension from
your MP plugin, for example:

```lua
if extensions and extensions.NL_bridge then
  extensions.NL_bridge.onPlayerConnected(id, name)
end
```

Exact `extensions.*` module name depends on how BeamNG loads the file (`NL/bridge` → often
`NL_bridge`). Check `extensions.getList()` in-game if unsure.

Until those hooks fire, treat multiplayer join gate as not ready.

## Kick path (best-effort)

Order when Session Host / `NL.Server` sends UDP `kick` / `despawn`:

1. Resolve player id from the bridge name map / `MPVehicleGE.getPlayerByName` / `MP.GetPlayers`.
2. Try in-process `MP.DropPlayer(id, reason)` or legacy `DropPlayer(id)` if present in the GE Lua VM.
3. Always append a line to `%LOCALAPPDATA%\NL\beamng-kicks.ndjson` for `NL_Kick`.
4. If nothing works → toast + local `recover` (solo fallback) and log `kick fallback`.

`MP.DropPlayer` is a **server-side** BeamMP API. The authoritative kick for dedicated hosts is
step 3 (`NL_Kick` plugin). In-process DropPlayer only succeeds when that API is exposed in
the same Lua VM as the GE bridge (uncommon on stock client builds).

## Verify join-gate kick

```powershell
# Seed a banned Guest, then replay the BeamMP join sample (dry-run):
powershell -File scripts/seed-banned-eve.ps1   # or ban Guest in Moderation Console
dotnet run --project src/NL.Server -- --game generic `
  --config samples/configs/beamng.nle `
  --source samples/events/beamng-beammp-join-sample.ndjson `
  --replay --join-gate --beamng-cmd 127.0.0.1:27022
```

Live: ban the exact in-game name → have them join → confirm kick queue line + disconnect.
