# NL Game Integration Spec v1

This is the **canonical contract** for connecting any game, mod, or server to NexoraLive (NL)
without game-specific C# code in the NL repository.

Implement this spec in your engine bridge; run NL with `--game generic` and a matching `.nle`
config.

**Related code:** `src/NL.Server.Core/Integration/`, `src/NL.Server/Integration/`

---

## Overview

```text
  ┌──────────────┐     events (NDJSON)      ┌─────────────┐
  │ Game bridge  │ ───────────────────────► │  NL.Server  │
  │ (your code)  │ ◄─────────────────────── │ RuleEngine  │
  └──────────────┘     actions (NDJSON)      └─────────────┘
```

NL evaluates events against a streamer-authored `.nle` file and returns **Allow**, **Block**,
or **Warn**. On **Block**, NL sends a standard **action** back to your bridge (warn, kick,
recover, tell, …).

---

## Protocol version

| Field | Value |
|-------|--------|
| Version | `1` |
| JSON field | `"nl": 1` (optional on events; recommended) |
| Action type | `"type": "action"` |

Lines without `"nl"` remain valid (backward compatible with pre-v1 NDJSON).

---

## Event envelope (bridge → NL)

One **UTF-8 NDJSON line** per event (newline `\n` delimited).

### Required

| Field | Type | Description |
|-------|------|-------------|
| `event` | string | Event name matched by `.nle` (`event foo:` blocks) |

### Recommended

| Field | Type | Description |
|-------|------|-------------|
| `nl` | number | Protocol version (`1`) |
| `player` | string | Player / driver / character id |
| `ts` | number | Unix time in **milliseconds** (anti-cheat replay + rate windows) |
| `props` | object | Numeric/bool gameplay properties |

### Example

```json
{"nl":1,"event":"shoot","player":"Alice","ts":1700001000000,"props":{"weapon.damage":12,"player.alive":1}}
```

### Comments

Lines starting with `#` (after optional whitespace) are ignored.

### Property rules

- `props` values: numbers, booleans (`true`→1, `false`→0), or numeric strings
- Use dotted keys for namespacing: `player.x`, `vehicle.speed`, `anomaly.severity`

### Conventional event names

Use names your `.nle` config expects. Common vocabulary:

| Event | Typical use |
|-------|-------------|
| `sessionStart` / `sessionEnd` | Map / round lifecycle |
| `playerJoin` / `playerLeave` | Connect / disconnect |
| `playerChat` | Chat message |
| `playerDeath` / `respawn` | Life cycle |
| `move` | Throttled pose / speed samples |
| `shoot`, `useItem`, `attack` | Gameplay actions |
| `anomalyImpossibleAction`, `anomalyRateSpike`, `anomalyTeleport` | Anti-cheat (NL can also emit these) |

You may define **any** event names — the rule engine only cares about names in your `.nle`.

---

## Action envelope (NL → bridge)

Sent when NL **Blocks** (and optionally configurable for Warn later). One NDJSON line per action.

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `nl` | number | `1` |
| `type` | string | Always `"action"` |
| `action` | string | Standard verb (see below) |
| `player` | string | Target player |
| `event` | string | Blocked event name |
| `decision` | string | `"Block"` |
| `message` | string | Rule message / reason |
| `ts` | number | Unix ms when NL emitted the action |

### Example

```json
{"nl":1,"type":"action","action":"warn","player":"Alice","event":"move","decision":"Block","message":"Too fast","ts":1700001000123}
```

### Standard action verbs

| Action | Meaning | Bridge should |
|--------|---------|---------------|
| `warn` | Soft enforcement | Toast / HUD message |
| `tell` | Direct message | Chat / DM to player |
| `kick` | Remove from session | Kick / disconnect |
| `recover` | Undo bad state | Teleport / reset vehicle / respawn |
| `mute` | Silence player | Mute chat/voice |
| `despawn` | Remove entity | Despawn without full kick |
| `custom` | Game-specific | Interpret using `message` |

NL chooses a default action from the blocked event name (override in bridge if needed):

| Event pattern | Default action |
|---------------|----------------|
| `playerJoin` | `kick` |
| `playerChat` | `tell` |
| `anomaly*`, `crash`, `rollover`, `leaveBoundary`, `airtime` | `recover` |
| (else) | `warn` |

---

## Transports

### 1. File append (legacy / simple)

Bridge appends event lines to a file; NL tails it:

```bash
NL.Server --game generic --config game.nle --source /path/to/events.ndjson
```

**Limitation:** some game sandboxes block writes outside their install folder. Prefer TCP/WS.

### 2. TCP event ingest (recommended for native bridges)

NL **listens**; bridge **connects** as client and sends newline-delimited event lines.

```bash
NL.Server --game generic --config game.nle --source tcp://127.0.0.1:27021
```

| Setting | Default |
|---------|---------|
| Host | `127.0.0.1` |
| Port | `27021` |

**Actions (split channel):** bridge listens on TCP; NL connects outbound:

```bash
NL.Server ... --source tcp://127.0.0.1:27021 --nl-action tcp://127.0.0.1:27023
```

Default action port: `27023`.

### 3. WebSocket bidirectional (recommended for modern bridges)

NL **listens** for WebSocket upgrade; events and actions share one socket.

```bash
NL.Server --game generic --config game.nle --source ws://127.0.0.1:27021/nl/v1 --nl-action auto
```

| Setting | Value |
|---------|--------|
| URL | `ws://127.0.0.1:27021/nl/v1` |
| HTTP listen prefix | `http://127.0.0.1:27021/` |
| Path | `/nl/v1` (conventional) |
| `--nl-action auto` | Required for outbound actions on same socket |

**Framing:** UTF-8 text WebSocket messages; one or more NDJSON lines per message (split on `\n`).

---

## Quick start

### 1. Start NL (WebSocket)

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/generic.nle \
  --source ws://127.0.0.1:27021/nl/v1 \
  --nl-action auto \
  --anti-cheat
```

### 2. Run a reference bridge

```bash
python integrations/python/nl_bridge.py --url ws://127.0.0.1:27021/nl/v1 --sample
```

### 3. Or replay a file (no bridge)

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/generic.nle \
  --source samples/events/generic-sample.ndjson \
  --replay
```

---

## Reference bridges

Copy-ready templates live under [`integrations/`](../integrations/README.md):

| Path | Engine |
|------|--------|
| `integrations/python/nl_bridge.py` | Any (Python + websocket-client) |
| `integrations/nodejs/nl_bridge.mjs` | Any (Node 18+) |
| `integrations/lua/nl_bridge_minimal.lua` | BeamNG / generic Lua |
| `integrations/minecraft/paper/` | Paper plugin stub |
| `integrations/unity/NLBridge.cs` | Unity C# |
| `integrations/unreal/NLBridge.h` | Unreal C++ stub |
| `integrations/generic/log_to_ndjson.ps1` | Log → NDJSON converter |

---

## Legacy adapters

These remain supported but are **not** the v1 standard:

| Adapter | Notes |
|---------|--------|
| Minecraft log tail | `--game minecraft --source latest.log` |
| BeamNG UDP `SCBN1` | `--beamng-cmd 127.0.0.1:27022` (use NL v1 WS/TCP for new work) |
| Shell `--action-cmd` | Escape hatch for any game |

New integrations should implement **NL Integration Spec v1** (this document).

---

## Validation checklist

- [ ] Bridge emits valid NDJSON with `event` field
- [ ] Optional `"nl": 1` on events
- [ ] `ts` present for anti-cheat-sensitive titles
- [ ] Bridge connects to NL before gameplay events fire
- [ ] Bridge reads action lines and applies `warn` / `kick` / `recover`
- [ ] `.nle` event names match bridge event names exactly
- [ ] Smoke test: `scripts/nl-integration-smoke.ps1`

---

## Version history

| Version | Date | Changes |
|---------|------|---------|
| 1 | 2026-07-21 | Initial spec: NDJSON events/actions, file/tcp/ws transports |
