# BeamMP notes for NL_BeamNGBridge

Solo freeroam works without BeamMP. Multiplayer join gate needs player identity events.

## Host setup

1. Host runs BeamNG + BeamMP + this mod + NL Session Host (join gate **on**).
2. Confirm `playerJoin` lines appear in `%LOCALAPPDATA%\NL\beamng-events.ndjson` for remote
   names (prop `beammp: 1`).
3. Ban a name in Moderation Console → that player's next join should UDP `kick`.

## Hook wiring

`lua/ge/extensions/NL/bridge.lua` exposes:

- `onPlayerConnected(playerId, name)`
- `onPlayerDisconnected(playerId, name)`

If your BeamMP plugin uses different callback names, call into the loaded NL extension from
your MP plugin, for example:

```lua
if extensions and extensions.NL_bridge then
  extensions.NL_bridge.onPlayerConnected(id, name)
end
```

Exact `extensions.*` module name depends on how BeamNG loads the file (`NL/bridge` → often
`NL_bridge`). Check `extensions.getList()` in-game if unsure.

## Kick vs recover

UDP `kick` / `despawn` currently toast + recover locally. Replace `despawnOrKick` in
`bridge.lua` with BeamMP server kick/despawn once you have a stable host API.
