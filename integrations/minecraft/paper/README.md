# NL Paper plugin stub

Implement [**NL Game Integration Spec v1**](../../../docs/NL_INTEGRATION_SPEC.md) in a Paper plugin.

## Approach

1. **WebSocket (recommended):** connect to `ws://127.0.0.1:27021/nl/v1` on enable.
2. **Or file tail:** append to `%LOCALAPPDATA%/NL/minecraft-events.ndjson` (legacy).

## Events to emit

| Bukkit event | NL `event` |
|--------------|------------|
| `PlayerJoinEvent` | `playerJoin` |
| `PlayerQuitEvent` | `playerLeave` |
| `AsyncPlayerChatEvent` | `playerChat` |
| `PlayerDeathEvent` | `playerDeath` |
| `PlayerRespawnEvent` | `respawn` |

Example line:

```json
{"nl":1,"event":"playerJoin","player":"Steve","ts":1700001000000,"props":{"player.alive":1}}
```

## Actions to handle

Parse inbound WebSocket text frames (NDJSON). On `"action":"kick"`, call `player.kick(Component.text(message))`.

## Starter class

See [`NLBridgePlugin.java`](src/main/java/nl/example/NLBridgePlugin.java) — compile with Paper API in your own Gradle project (not built by NL.sln).

## NL.Server

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/minecraft.nle \
  --source ws://127.0.0.1:27021/nl/v1 \
  --nl-action auto --join-gate --anti-cheat
```

Or keep using log tail + RCON (`--game minecraft`) until the plugin is ready.
