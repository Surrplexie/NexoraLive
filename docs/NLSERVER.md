# NLServer — Phase 3 (complete): game-agnostic real server integration

Phase 3 replaces `MockGameEventSource` with **real session events** from a running (or
recorded) game server, evaluated by the same Phase 0 `RuleEngine`, with optional real
actions on `Block`.

This is **not** Minecraft-only. The host is game-agnostic: Minecraft is one built-in
adapter; any other game integrates via [**NL Integration Spec v1**](NL_INTEGRATION_SPEC.md)
(NDJSON over file, TCP, or WebSocket — no C# adapter required).

## Architecture

```mermaid
flowchart LR
  src["IGameEventSource<br/>(minecraft log / NDJSON / custom)"] --> host[NlServerHost]
  cfg[".nle + RuleEngine"] --> host
  host --> decision[ActionResult]
  decision --> sink["IGameActionSink<br/>(RCON / process / dry-run)"]
```

| Piece | Role |
|---|---|
| `IGameEventSource` | Yields `SessionEvent`s (`GameEvent` + optional player name) |
| `IGameActionSink` | Applies Block decisions back to the game |
| `NlServerHost` | Shared evaluate loop — no game-specific code |
| `SessionEvent` | `GameEvent` stays "name + numeric props"; player id stays alongside |

`RuleEngine` was **not** changed.

## Built-in games

### `minecraft`

Tails or replays a vanilla/Paper Java server `logs/latest.log`.

```bash
dotnet run --project src/NL.Server -- --game minecraft --config samples/configs/minecraft.nle --source samples/logs/minecraft-sample.log --replay
```

Live with RCON:

```bash
dotnet run --project src/NL.Server -- --game minecraft --config samples/configs/minecraft.nle --source C:\mc\logs\latest.log --rcon 127.0.0.1:25575:secret
```

Event vocabulary: `playerJoin`, `playerLeave`, `playerChat`, `playerDeath`, `playerAdvancement`
(see older [NLSERVER_MINECRAFT.md](NLSERVER_MINECRAFT.md) table; death templates expanded).

### `generic` — any game (NL Integration Spec v1)

Any engine/mod/plugin sends **NDJSON event lines** and receives **NDJSON action lines**.
Full spec: [NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md).

**File (legacy):**

```json
{"nl":1,"event":"shoot","player":"Alice","ts":1700001000000,"props":{"weapon.damage":12}}
```

```bash
dotnet run --project src/NL.Server -- --game generic --config samples/configs/generic.nle --source samples/events/generic-sample.ndjson --replay
```

**TCP listen** (bridge connects to NL):

```bash
dotnet run --project src/NL.Server -- --game generic --config samples/configs/generic.nle --source tcp://127.0.0.1:27021 --nl-action tcp://127.0.0.1:27023
```

**WebSocket bidirectional** (recommended):

```bash
dotnet run --project src/NL.Server -- --game generic --config samples/configs/generic.nle --source ws://127.0.0.1:27021/nl/v1 --nl-action auto --anti-cheat
```

Reference bridges: [`integrations/`](../integrations/README.md).

## Actions on Block

| Flag | Behavior |
|---|---|
| *(none)* | Dry-run: print what would be applied |
| `--rcon host:port:password` | Source RCON (Minecraft defaults: join→`kick`, else→`tell`) |
| `--rcon-cmd "say blocked {player}"` | Custom RCON template (`{player}` `{event}` `{decision}` `{message}`) |
| `--nl-action auto` | Bidirectional WebSocket actions (with `ws://` source) — NL Integration Spec v1 |
| `--nl-action tcp://host:port` | TCP action lines to bridge listener (split channel) |
| `--action-cmd "..."` | Shell process for non-RCON games (same placeholders) |

## CLI

```
NL.Server --game <minecraft|generic> --config <file.nle> --source <path>
          [--replay] [--rcon host:port:password] [--rcon-cmd "..."] [--action-cmd "..."]
          [--anti-cheat]
```

`--replay` reads the whole source from the start and exits (CI / demos). Default is live
follow from the current end of the file. `--anti-cheat` wraps the source with Phase 5
anomaly detectors — see [ANTICHEAT.md](ANTICHEAT.md).

Legacy positional args still work as Minecraft live mode:
`NL.Server <config.nle> <log-path> [rconHost] [rconPort] [rconPassword]`.

## Where the code lives

- `src/NL.Server.Core/` — interfaces, `NlServerHost`, Minecraft parser/mapper, generic NDJSON
  parser, RCON packet framing, recording/null sinks
- `src/NL.Server/` — file reader, line event source factories, RCON/process/console sinks, CLI
- `samples/logs/minecraft-sample.log`, `samples/events/generic-sample.ndjson`
- `samples/configs/minecraft.nle`, `samples/configs/generic.nle`
- `tests/NL.Server.Core.Tests/` — parsers, mapper, host, RCON wire format

## Phase 3 completion status

- [x] Game/engine target decided: **pluggable adapters**, with Minecraft + universal NDJSON
- [x] Real event vocabulary for Minecraft; arbitrary for generic
- [x] Wired into existing `RuleEngine` with no engine changes
- [x] End-to-end validated via `--replay` against checked-in sample sources (both adapters)
- [x] Real action channels: RCON + generic process sink + dry-run
- [x] Broader Minecraft death-message coverage
- [ ] Optional later: native adapters (Paper plugin, Source engine log, etc.) — not required;
      NDJSON covers "any game" without new C# per title
