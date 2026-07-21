# NL reference bridges

Starter templates for [**NL Game Integration Spec v1**](../docs/NL_INTEGRATION_SPEC.md).

Pick the template closest to your engine, copy it into your mod/plugin, and wire event names
to match your `.nle` config.

## Layout

| Directory | Use when |
|-----------|----------|
| [`python/`](python/nl_bridge.py) | Fastest smoke test; any OS with Python 3 |
| [`nodejs/`](nodejs/nl_bridge.mjs) | Node/Electron tooling |
| [`lua/`](lua/nl_bridge_minimal.lua) | BeamNG, Garry's Mod, other Lua sandboxes |
| [`minecraft/paper/`](minecraft/paper/) | Paper/Spigot Java server plugin |
| [`unity/`](unity/NLBridge.cs) | Unity games (client or dedicated server) |
| [`unreal/`](unreal/NLBridge.h) | Unreal Engine mods |
| [`godot/`](godot/nl_bridge.gd) | Godot 4 (WebSocketPeer) |
| [`rust/`](rust/) | Rust CLI bridge (`cargo run -- --url ws://… --sample`) |
| [`dotnet/NlBridge/`](dotnet/NlBridge/) | .NET 8 console WebSocket bridge |
| [`generic/`](generic/log_to_ndjson.ps1) | Existing text logs → NDJSON file |

## Smoke test (WebSocket)

Terminal 1 — start NL:

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/generic.nle \
  --source ws://127.0.0.1:27021/nl/v1 \
  --nl-action auto
```

Terminal 2 — reference bridge:

```bash
pip install websocket-client
python integrations/python/nl_bridge.py --url ws://127.0.0.1:27021/nl/v1 --sample
```

NL should log Allow/Block decisions; the Python bridge prints inbound actions.

Or run `scripts/nl-integration-smoke.ps1` (starts NL + bridge automatically).

## Session bus (Phase B)

Start the cross-platform web Session Host (dashboard + authenticated bridge):

```bash
dotnet run --project src/NL.SessionHost.Web
```

Open `http://127.0.0.1:27020`, copy the **bridge URL** (includes `?token=`), and point your mod at it.
See [docs/NL_SESSION_BUS.md](../docs/NL_SESSION_BUS.md) and `scripts/nl-session-bus-smoke.ps1`.

## TCP split-channel example

Terminal 1:

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/generic.nle \
  --source tcp://127.0.0.1:27021 \
  --nl-action tcp://127.0.0.1:27023
```

Terminal 2:

```bash
python integrations/python/nl_bridge.py --tcp-events 127.0.0.1:27021 --tcp-actions 127.0.0.1:27023 --sample
```

## Writing your bridge

1. Emit sparse, semantic events (not every frame tick unless `.nle` needs it).
2. Include `"nl": 1` and `"ts"` (Unix ms).
3. Use `props` for numeric gameplay state (`player.alive`, coordinates, speed, …).
4. Handle inbound action lines — at minimum `warn` and `kick`.
5. Keep a reconnect loop — NL may restart between sessions.

See the spec for full field definitions and default action mapping.
