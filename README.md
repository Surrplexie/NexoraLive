# NexoraLive (NL)

**Status:** early prototype — usable for local demos, replay validation, and Windows-side tooling.  
**Not yet:** a finished product, hosted multiplayer platform, or drop-in replacement for game-specific moderation stacks.

NexoraLive is a streamer-oriented session rules system. You author plain-text `.nle` configs; a shared rule engine evaluates gameplay (or hotkey) events and returns **Allow**, **Block**, or **Warn**. This repository implements that core loop plus the first real integrations around it.

For the long-form NLE walkthrough, see [`NLE_GUIDE.md`](NLE_GUIDE.md). For build vs. plan, see [`ROADMAP.md`](ROADMAP.md).

---

## What works today

| Area | What you can do | Maturity |
|------|-----------------|----------|
| **NLEvents language + rule engine** | Parse `.nle`, evaluate events, unit-test decisions | Solid prototype |
| **Simulator** | Run mock events against a config; no game required | Ready for learning |
| **Hotkey Daemon (Windows)** | Real global hotkeys, mic mute, OBS clip, tray UX | Daily-use hardening |
| **Config Editor (Windows)** | Visual `.nle` authoring + live rule preview | Usable |
| **SP join eligibility** | Standing / roles / offenses → Allow / Deny / Hold | Model + simulators |
| **NLServer** | Minecraft log or generic NDJSON → rules → optional RCON / process / UDP / **NL v1 TCP/WS** actions | Live-capable, still early |
| **Moderation Console (Windows)** | Audit log + warn / ban / graylist / clear | Basic admin UI |
| **Anti-cheat (early)** | Session-path anomaly signals (`anomaly*`) evaluated by the same `.nle` engine — see [Anti-cheat direction](#anti-cheat-direction) | Signal prototype; full packet path WIP |
| **Session Host (Windows)** | One Start/Stop shell for a full session profile | Recommended entry for live |
| **BeamNG.drive bridge** | Lua mod → NDJSON → rules → localhost UDP + BeamMP kick queue | Freeroam / BeamMP operator path |

**Not implemented yet:** hosted NLServers with in-path packet anti-play, economy / trading cards, mobile clients, cloud hosting, encrypted `.nle` packaging, and most of the broader platform vision. Treat this repo as an **early working guide**: enough to learn the model, validate configs, and dogfood a few real sessions — not a finished operator product.

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer
- **Windows** for: Hotkey Daemon, Session Host, Moderation Console, Config Editor
- **Any OS with the SDK** for: `NL.Core`, simulators, `NL.Server` CLI, unit tests
- Optional for live Minecraft: Java server with **RCON** enabled
- Optional for BeamNG: BeamNG.drive + the bundled Lua bridge mod

Clone this repository and work from the repo root in the commands below.

---

## 1. Build and verify

```bash
dotnet build src/NL.sln
dotnet test src/NL.sln
```

If both succeed, the prototype toolchain is healthy on your machine.

---

## 2. First run (no game required)

### Rule engine simulator

Loads a `.nle` file and runs a fixed script of **mock** events:

```bash
dotnet run --project src/NL.Simulator
dotnet run --project src/NL.Simulator -- samples/configs/full-session.nle
```

Example output shape:

```
- PlayerA fires a weapon
    event:    shoot
    decision: Block
```

Edit any file under `samples/configs/`, re-run, and confirm decisions change. This is the fastest way to learn the language before wiring a real game.

### StreamPlayer join simulator

```bash
dotnet run --project src/NL.SpSimulator
```

Prints Allow / Deny / Hold for mocked SP profiles against join requirements. See [`docs/SP_MODEL.md`](docs/SP_MODEL.md).

### Replay a recorded session (CLI)

Prefer `--replay` until you are ready for live side effects:

```bash
# Minecraft sample log
dotnet run --project src/NL.Server -- --game minecraft \
  --config samples/configs/minecraft.nle \
  --source samples/logs/minecraft-sample.log \
  --replay

# Generic NDJSON sample
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/generic.nle \
  --source samples/events/generic-sample.ndjson \
  --replay

# Anti-cheat sample (Alice clean / Eve anomalous) — early signal path
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/anti-cheat.nle \
  --source samples/events/anti-cheat-sample.ndjson \
  --replay --anti-cheat

# BeamNG sample
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/beamng.nle \
  --source samples/events/beamng-sample.ndjson \
  --replay --anti-cheat
```

The `--anti-cheat` flag turns on the **current** anomaly detectors (impossible action, rate spike, teleport). That is the early stand-in for the longer-term server / in-game anti-play path described below — not a finished packet gate.

---

## 3. Authoring rules (`.nle`)

A config is plain text: optional `hotkey` bindings, then `event` blocks with `allow` / `block` / `warn` / `if` (including `and` / `or`).

```nle
# No PvP shooting during this session
event shoot:
    block

event respawn:
    if player.health > 0:
        block
    else:
        allow

event leaveBoundary:
    warn "stay within the zone"
    block
```

| Resource | Purpose |
|----------|---------|
| [`docs/NLEVENT_LANGUAGE_SPEC_v0.1.md`](docs/NLEVENT_LANGUAGE_SPEC_v0.1.md) | Grammar for v0.1 |
| [`NLE_GUIDE.md`](NLE_GUIDE.md) | Formal guide: concepts → daily use |
| `samples/configs/*.nle` | Copy-paste starting points |
| `dotnet run --project src/NL.ConfigEditor` | Visual editor + live evaluate (Windows) |

**Convention:** unknown events default to **Allow**. Be explicit about what you want blocked.

---

## 4. Windows tooling

### Hotkey Daemon

Real global hotkeys gated by the same rule engine:

```bash
dotnet run --project src/NL.HotkeyDaemon
```

- Default day-to-day config: `%LOCALAPPDATA%\NL\hotkeys.nle` (created from a starter template if missing)
- Repo sample while developing: `samples/configs/hotkeys.nle`
- Actions include mic mute, announce, master enable/disable, open log, OBS clip, focus OBS, mute desktop
- Details: [`docs/HOTKEY_DAEMON.md`](docs/HOTKEY_DAEMON.md)

### Config Editor

```bash
dotnet run --project src/NL.ConfigEditor
```

Build hotkey bindings and event rules visually; preview Allow/Block before saving.

### Moderation Console

```bash
dotnet run --project src/NL.ModerationConsole
```

Review the audit trail and issue warning / ban / graylist / clear against SP profiles. Shared data lives under `%LOCALAPPDATA%\NL\` (see below).

### Session Host (recommended live entry)

```bash
dotnet run --project src/NL.SessionHost
```

One Start/Stop UI for a session profile: game adapter, `.nle` path, event source, optional RCON / BeamNG UDP, join gate, early anti-cheat signals, anomaly auto-mod. Tools menu opens Moderation Console and Config Editor when published side-by-side.

**Cross-platform (Phase B):** web dashboard + session bus:

```bash
dotnet run --project src/NL.SessionHost.Web
```

Open `http://127.0.0.1:27020` — REST API, bridge WebSocket on port 27021 with token auth. See [docs/NL_SESSION_BUS.md](docs/NL_SESSION_BUS.md).

### Portable publish layout

```powershell
powershell -File scripts/publish.ps1
```

Writes `artifacts/publish/{SessionHost,SessionHostWeb,ModerationConsole,ConfigEditor,HotkeyDaemon,Server}`. Zip that folder for a simple portable install.

---

## 5. Live paths (early / careful)

These paths can affect a real game session. Start in **dry-run** or **replay** mode first.

### Shared local data

| Path | Purpose |
|------|---------|
| `%LOCALAPPDATA%\NL\sp-profiles.json` | SP standing / offenses |
| `%LOCALAPPDATA%\NL\moderation.jsonl` | Decision + mod-action audit log |
| `%LOCALAPPDATA%\NL\join-requirements.json` | Join gate requirements |
| `%LOCALAPPDATA%\NL\session-profile.json` | Last Session Host profile |
| `%LOCALAPPDATA%\NL\hotkeys.nle` | Daemon config |
| `%LOCALAPPDATA%\NL\hotkeys.log` | Daemon action log |

Default streamer id is `default-streamer` unless you override it.

### Minecraft (Java)

1. Enable RCON in `server.properties` (`enable-rcon`, `rcon.port`, `rcon.password`).
2. Open Session Host → game `minecraft` → point **Config** at `samples/configs/minecraft.nle` (or your file) → **Source** at the server `logs/latest.log`.
3. Leave RCON empty for dry-run; fill `host:port:password` when you want kicks/tells.
4. Enable **Join gate** and **Anti-cheat** for the early anomaly loop; optionally **Anomaly auto-mod** (severity ≥ 2 Block → graylist). Richer `.nle` rules here matter: anti-cheat decisions are meant to follow your NLEvents, not a separate black-box ban list.

CLI equivalent:

```bash
dotnet run --project src/NL.Server -- --game minecraft \
  --config samples/configs/minecraft.nle \
  --source "C:\path\to\logs\latest.log" \
  --rcon 127.0.0.1:25575:your-secret \
  --streamer default-streamer \
  --join-gate --anti-cheat
```

Prove join gate: ban a test account in Moderation Console (exact in-game name), have them join, confirm Block / kick. Full checklist: [`docs/MINECRAFT_LIVE.md`](docs/MINECRAFT_LIVE.md).

### BeamNG.drive

```powershell
powershell -File scripts/install-beamng-bridge.ps1
```

Then Session Host → **Tools → Load BeamNG freeroam defaults** (generic NDJSON, `beamng.nle`, freeroam anti-cheat thresholds via `--beamng-cmd`, join gate off for solo). Events append to `%LOCALAPPDATA%\NL\beamng-events.ndjson`; Blocks return over UDP `127.0.0.1:27022`. BeamMP kicks use `scripts/install-beammp-nl-kick.ps1` + the kick queue. Guide: [`docs/BEAMNG.md`](docs/BEAMNG.md).

### Join-gate demo without Minecraft

```powershell
powershell -File scripts/seed-banned-eve.ps1
```

Seeds a banned Eve profile under `%LOCALAPPDATA%\NL` and can replay the join-gate sample.

---

## 6. How the pieces fit

```text
.nle config  →  Lexer / Parser  →  RuleEngine
                                      ↑
     Minecraft log | NDJSON | hotkeys | mock events
                                      ↓
                         Allow / Block / Warn
                                      ↓
              RCON | process | UDP | tray action | console
```

- **Same engine** for simulator, daemon, and NLServer — configs are not siloed per app.
- **Game adapters** only produce `GameEvent`s (name + properties); they do not reimplement rules.
- **Anti-cheat** is meant to sit on the session path (player input → check → NLServer → in-game). Today that is an early anomaly signal layer; the target is NLE-driven enforcement of gameplay/network signatures. See below.
- **Moderation** records decisions and can change SP standing, which the join gate reads on the next join.

Architecture notes: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). Server details: [`docs/NLSERVER.md`](docs/NLSERVER.md).

### Anti-cheat direction

NexoraLive anti-cheat is **not** a kernel-level or memory-scanner product, and it is **not** primarily a “ban every hacker” client. The intended model is **server- and session-path anti-play**: when a streamer runs sessions through NLServers, player actions (and, later, network packet signatures) are checked against the streamer’s **NLEvents** before they become in-game outcomes.

Intended flow (target):

```text
player input → anti-cheat check → NLServers / .nle rules → allowed in-game action
```

Example intent: a fly / impossible-motion style signature is rejected on the path to the game because it violates the session rules — not because a separate opaque AC decided to ban. The useful mental model is **override and gate via NLEvents** (more detailed `.nle` ⇒ better enforcement), not a standalone cheat-ban appliance for things like x-ray lists.

**What ships in this repo today**

| Today (prototype) | Direction (WIP) |
|-------------------|-----------------|
| `--anti-cheat` wraps the event source | Sits between player input and NLServers on the live path |
| Detectors emit `anomalyImpossibleAction` / `anomalyRateSpike` / `anomalyTeleport` | Inspect gameplay / packet signatures against session rules |
| You author `.nle` blocks for those `anomaly*` names | Same idea: streamer `.nle` decides Allow / Block / Warn |
| Replay sample proves the loop without a live game | Hosted NLServers apply checks before in-game effect |

Try the early path:

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/anti-cheat.nle \
  --source samples/events/anti-cheat-sample.ndjson \
  --replay --anti-cheat
```

Detector vocabulary and wiring: [`docs/ANTICHEAT.md`](docs/ANTICHEAT.md).

---

## Repository layout

| Path | Role |
|------|------|
| `src/NL.Core` | Language + `RuleEngine` + SP model |
| `src/NL.Simulator` / `NL.SpSimulator` | Mock CLIs |
| `src/NL.HotkeyDaemon` (+ `.Core`) | Windows hotkey tray app |
| `src/NL.ConfigEditor` | Visual `.nle` editor |
| `src/NL.Server` (+ `.Core`) | Session host CLI |
| `src/NL.Moderation` (+ `.Core`, Console) | Audit store + admin UI |
| `src/NL.AntiCheat.Core` | Early anti-cheat anomaly detectors (session-path signals) |
| `src/NL.SessionHost` | Windows Start/Stop session shell |
| `src/NL.SessionHost.Web` | Cross-platform session bus + web dashboard |
| `tests/` | Unit tests |
| `samples/` | Safe example configs, logs, NDJSON (no real secrets) |
| `beamng-mod/` | BeamNG Lua bridge |
| `integrations/` | NL Integration Spec v1 reference bridges (Python, Node, Lua, …) |
| `docs/NL_INTEGRATION_SPEC.md` | Universal game integration contract |
| `scripts/` | Publish, bridge install, join-gate seed |
| `docs/` | Specs and operator notes |

---

## Suggested learning path

1. `dotnet test` — confirm the environment.
2. Run `NL.Simulator` against `samples/configs/full-session.nle`, then edit the file.
3. Read the language spec and skim `NLE_GUIDE.md`.
4. Replay Minecraft / generic samples, then the anti-cheat sample (`--anti-cheat`) to see NLE-gated anomaly Blocks.
5. On Windows: try Hotkey Daemon and Config Editor.
6. Only then attempt Session Host against a real Minecraft log or BeamNG freeroam — dry-run first.

---

## Documentation index

| Doc | Topic |
|-----|--------|
| [`NLE_GUIDE.md`](NLE_GUIDE.md) | Formal NLE guide (install → author → run → troubleshoot) |
| [`ROADMAP.md`](ROADMAP.md) | Phases built vs. planned |
| [`docs/NLEVENT_LANGUAGE_SPEC_v0.1.md`](docs/NLEVENT_LANGUAGE_SPEC_v0.1.md) | `.nle` grammar |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Core pipeline |
| [`docs/HOTKEY_DAEMON.md`](docs/HOTKEY_DAEMON.md) | Daemon actions and caveats |
| [`docs/SP_MODEL.md`](docs/SP_MODEL.md) | StreamPlayer + join eligibility |
| [`docs/NLSERVER.md`](docs/NLSERVER.md) | Game-agnostic server host |
| [`docs/NLSERVER_MINECRAFT.md`](docs/NLSERVER_MINECRAFT.md) | Minecraft adapter notes |
| [`docs/MODERATION.md`](docs/MODERATION.md) | Audit trail + console |
| [`docs/ANTICHEAT.md`](docs/ANTICHEAT.md) | Early anti-cheat signals (`anomaly*`); see also [Anti-cheat direction](#anti-cheat-direction) |
| [`docs/MINECRAFT_LIVE.md`](docs/MINECRAFT_LIVE.md) | Live Minecraft checklist |
| [`docs/BEAMNG.md`](docs/BEAMNG.md) | BeamNG bridge |

---

## License

MIT — see [LICENSE](LICENSE).
