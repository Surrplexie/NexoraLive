# NexoraLive (NL)

**Status:** active prototype — local demos, replay validation, Windows tooling, and a growing **fork-platform control plane** (identity, social gate, fork catalog, fork runtime).  
**Not yet:** production hosted fork fleet, full client overlay, or publisher SDK integrations in the wild.

NexoraLive is a streamer-oriented session rules system. You author plain-text `.nle` configs; a shared rule engine evaluates gameplay (or hotkey) events and returns **Allow**, **Block**, or **Warn**. This repository implements that core loop plus integrations toward **NL Server** (session control) and **NL Fork** (licensed game snapshots on NL infrastructure).

For the long-form NLE walkthrough, see [`NLE_GUIDE.md`](NLE_GUIDE.md). For build vs. plan, see [`ROADMAP.md`](ROADMAP.md). For the fork-platform architecture, see [`docs/NL_FORK_PLATFORM.md`](docs/NL_FORK_PLATFORM.md).

---

## NL Server and NL Fork

NL separates **who may join and what rules apply** from **where the game actually runs**. Both are required for streamer community sessions that publisher matchmaking cannot enforce today.

### NL Server (NLS) — control plane

**NL Server** is the session control layer: rules, moderation, join admission, identity, and the session bus that game bridges connect to.

| Responsibility | What it does |
|----------------|--------------|
| **Rule enforcement** | Evaluates `.nle` configs via the shared `RuleEngine` (Allow / Block / Warn) |
| **Join admission** | `JoinEligibilityEngine` — standing, follow/sub, offenses, graylist hold |
| **Identity** | Platform ownership proof (Steam, Epic, …) before admit |
| **Social gate** | Live follow/sub/discord checks; live-only session start |
| **Moderation** | Audit trail, warn/ban/graylist, SP offense history |
| **Session bus** | WebSocket `/nl/v1` — remote bridges emit events and receive actions |

**Ships today:** `NL.Server` CLI, `NL.SessionHost.Web` (operator dashboard, join gate UI, moderation console), admit API, manifest for remote bridges. See [`docs/NL_SESSION_SERVER.md`](docs/NL_SESSION_SERVER.md).

NL Server does **not** replace a game's executable. It sits **in front of** join and **beside** gameplay via integration bridges (Minecraft log, BeamNG Lua mod, NL Integration Spec v1, hello-fork runtime).

### NL Fork — data plane

**NL Fork** is a **major-version snapshot** of a title running on **NL-controlled infrastructure**. Players use normal licensed clients; server-side mods and NLE configs apply on the fork only — never pushed to viewer PCs.

| Responsibility | What it does |
|----------------|--------------|
| **Fork catalog** | Registered `gameId@major` rows (e.g. `gameA@1.0`) with partnership tier and image digest |
| **Fork runtime** | In-process or containerized game instance; events → session bus → rules → actions |
| **Server-side mods** | Verified mod hub; hash-checked; baked into fork instance |
| **Ephemeral sessions** | World/save state discarded when the stream ends; **no progress transfer** to publisher servers |
| **Major-only versioning** | Catalog rows are `1.0`, `2.0` — not per-patch lines; patches roll into the current major image |

**Ships today:** `NL.Fork.Core`, `NL.Fork.Runtime`, `NL.Fork.Catalog`, `NL.Fork.Orchestrator`, `/fork-catalog.html`, `/fork-orchestrator.html`, mock + hello-fork validation path. See [`docs/NL_FORK_RUNTIME.md`](docs/NL_FORK_RUNTIME.md), [`docs/NL_FORK_CATALOG.md`](docs/NL_FORK_CATALOG.md), and [`docs/NL_FORK_ORCHESTRATOR.md`](docs/NL_FORK_ORCHESTRATOR.md).

### How they work together

```text
Streamer goes live
    → selects gameId@major from Fork Catalog
    → attaches .nle + verified server mods
    → NL Server validates ownership + social gate + catalog major
    → Fork instance starts (or bridge connects to existing host)
    → SP joins via NL admit URL (not native platform invite)
    → events flow: Fork → session bus → RuleEngine → actions back to Fork
Stream ends → fork terminated → only .nle, moderation, and metadata persist
```

Native game invites to NLS addresses **fail by design** — traffic is filtered at the NL join layer. SPs connect only through NL admission after passing standing and ownership checks.

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
| **Session Host Web** | Operator console, NL Client, join gate, fork catalog, partnerships, spectator demo | Usable (Phases B–R) |
| **Platform identity** | Game ownership verification at admit (mock + Steam Web API) | Phase L — prototype |
| **Live social gate** | Follow/sub/discord hydration, live-only NLS, offense archive UI | Phase M — prototype |
| **Fork catalog** | Major-version registry, partnership tiers, mod hub, game picker UI | Phase N — prototype |
| **Fork orchestrator** | Ephemeral fork provisioning (mock/process/docker), lifecycle + manifest connect | Phase O — prototype |
| **Publisher partnerships** | At-own-risk ack gate, Play on NL SDK spec, ban sync, platform opt-in | Phase Q — prototype |
| **NL Client shell** | Join flow, deep links, overlay, stray-invite block, mobile mod companion | Phase R — prototype |
| **Fleet operations** | Multi-region, relay/TURN, observability, autoscale, abuse, compliance | Phase S — prototype |
| **Fork runtime** | Hello-fork + session bus; server-side mod loader | Phase P — prototype |
| **BeamNG.drive bridge** | Lua mod → NDJSON → rules → localhost UDP + BeamMP kick queue | Freeroam / BeamMP operator path |

**Not implemented yet:** production fork fleet at scale, publisher SDK menu integrations, mobile NL Client overlay, cloud fleet ops, encrypted `.nle` packaging, and economy features (SrCs, SPt) from the long-form vision doc. Treat this repo as an **early working guide** — enough to learn the model, validate configs, and dogfood control-plane + hello-fork paths — not a finished operator product or a substitute for publisher-hosted multiplayer.

---

## For game publishers and platforms

This section is for **rights holders, platform operators, and legal/compliance teams** evaluating NexoraLive. NL is designed so that **legitimate partnership is the default path**; “at own risk” is a **fallback tier for titles without an agreement**, not a workaround to avoid publisher consent.

### What NL is

NexoraLive enables **streamer-hosted community sessions** with enforceable, streamer-authored rules — follow/sub requirements, standing-based join gates, server-side mod application, and NLE-driven moderation — on infrastructure the streamer (via NL) controls. It is **not** a game store, a crack client, or a progress-sync layer.

### What NL is not

| NL does not | Why it matters |
|-------------|----------------|
| Sell game copies, DLC, or in-game currency | Publisher monetization stays with the publisher |
| Bypass publisher or platform bans | Banned users remain blocked at the ownership/admission layer |
| Accept pirated or unlicensed clients | Ownership verification is required before join |
| Write session progress to publisher cloud saves, MMR, or live-service backends | Fork sessions are **ephemeral by design** |
| Modify retail game installs permanently | Fork snapshots run isolated; clients reconnect to publisher services normally after the session |
| Substitute for publisher anti-cheat on publisher matchmaking | NL fork sessions are **separate instances**, not injections into official servers |

### Partnership model

NL catalog entries carry a **partnership tier**. Publishers and platforms choose how deeply to integrate — NL does not require every title to launch as “at own risk.”

| Tier | Who opts in | Player experience | NL obligation |
|------|-------------|-------------------|---------------|
| **Official** | Publisher (SDK, API, or formal agreement) | “Play on NL” surfaced in-product or via approved channel; EULA-aligned copy | Publisher-approved snapshot, legal review, coordinated deprecation |
| **Platform** | Platform operator (e.g. per-app Steam flag) | NL may host opted-in app IDs under platform terms | Platform agreement; ownership via platform APIs |
| **At own risk** | **No publisher or platform agreement** | Clear banner: not endorsed; no progress transfer; user acknowledgment | NL + streamer only; used when Official/Platform path is unavailable |

**Important:** “At own risk” is **not** NL’s preferred or default operating mode for major titles. It exists so streamers and communities have a **disclosed, bounded fallback** when no partnership exists — with explicit legal copy, no progress transfer, and no implied endorsement. NL actively pursues **Official** and **Platform** tiers for cataloged games.

### Technical assurances for partners

1. **Ownership gate** — Join admission verifies platform identity and game ownership (Steam Web API and extensible verifiers) before Allow.
2. **Major-version discipline** — One catalog row per publisher major release (`1.0`, `2.0`); client major must match session major or join is denied.
3. **Snapshot registry** — Container image digests, min client version, and deprecation policy are recorded in the fork catalog (`NL.Fork.Catalog`).
4. **Server-side mods only** — Verified mod hub with hash checks; mods execute on the fork instance, not on player clients.
5. **Auditability** — Moderation JSONL, admit decisions, and session manifests are logged on the control plane.
6. **Invite filtering** — Players join via NL admission URLs; native platform invites to NLS endpoints are rejected by design (documented in [`docs/NL_SOCIAL_GATE.md`](docs/NL_SOCIAL_GATE.md)).

### Data handling

| Persisted after a session | Discarded with the fork |
|---------------------------|-------------------------|
| Streamer `.nle` config | World / save state on the fork |
| Moderation audit + SP standing | Session inventory |
| Join/offense metadata | Fork container volumes |
| Orchestrator audit (when built) | Any state that would sync to publisher live services |

NL sessions **must not** become a backdoor for cloud-save manipulation, ranked progression, or economy extraction on publisher backends.

### Engagement path for publishers and platforms

If you represent a **game publisher**, **platform store**, or **first-party multiplayer service** and want NL integration beyond the at-own-risk tier:

1. **Contact** — Open a discussion via your publisher/platform partnership channel (GitHub Discussions on this repo for technical preview; formal partnership inquiries should use your designated business contact when available).
2. **Scope** — Define allowed snapshot majors, branding, EULA snippets, ownership API, and whether Official or Platform tier applies.
3. **Catalog** — NL registers the title in the fork catalog as **Official** or **Platform** with agreed image digest, legal notice, and deprecation rules.
4. **Launch** — Streamers select the partnered entry in the fork catalog; users see tier-appropriate copy at join time; sessions remain ephemeral unless explicitly agreed otherwise.

NL welcomes **SDK hooks, menu entries, and co-marketing** for Official tier titles. NL does **not** require publishers to approve at-own-risk operation as a condition of technical conversation — the goal is to **replace** at-own-risk with a sanctioned path where both sides agree.

### Reference documents

| Document | Audience |
|----------|----------|
| [`docs/NL_FORK_PLATFORM.md`](docs/NL_FORK_PLATFORM.md) | Architecture: control plane vs data plane, lifecycle |
| [`docs/NL_FORK_CATALOG.md`](docs/NL_FORK_CATALOG.md) | Snapshot registry, tiers, major-only policy |
| [`docs/NL_IDENTITY.md`](docs/NL_IDENTITY.md) | Ownership verification and platform linking |
| [`docs/NL_SOCIAL_GATE.md`](docs/NL_SOCIAL_GATE.md) | Join gate, live-only sessions, invite policy |
| [`NexoraLive.txt`](NexoraLive.txt) | Long-form product vision (hypothetical / exploratory) |

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

**Cross-platform (Phase B–D):** web session server + session bus:

```bash
dotnet run --project src/NL.SessionHost.Web
```

Open `http://127.0.0.1:27020` — remote bridge manifest, join admission API, moderation at `/moderation.html`, join gate at `/join-gate.html`, fork catalog at `/fork-catalog.html`. See [docs/NL_SESSION_SERVER.md](docs/NL_SESSION_SERVER.md).

**Fork catalog (Phase N):** enable with `NL_FORK_CATALOG_ENABLED=true`, copy `samples/fork/catalog.json` to your catalog path, open `/fork-catalog.html` to select `gameId@major` and apply the default `.nle` template. See [docs/NL_FORK_CATALOG.md](docs/NL_FORK_CATALOG.md).

**Fork orchestrator (Phase O):** enable with `NL_FORK_ORCHESTRATOR_ENABLED=true` and `NL_FORK_ORCHESTRATOR_MODE=mock` (or `process` / `docker`). Set `forkOrchestratorEnabled: true` on the session profile, then start the session — the orchestrator provisions an ephemeral fork instance and exposes connect fields on the session manifest. See [docs/NL_FORK_ORCHESTRATOR.md](docs/NL_FORK_ORCHESTRATOR.md).

**Identity + social gate (Phases L–M):** see [docs/NL_IDENTITY.md](docs/NL_IDENTITY.md) and [docs/NL_SOCIAL_GATE.md](docs/NL_SOCIAL_GATE.md).

**Public demo (Phase E):** set `NL_PUBLIC_MODE=true` plus `NL_BUS_TOKEN` and `NL_OPERATOR_KEY` before exposing the server. Copy [`.env.example`](.env.example) and see [docs/NL_DEMO_SECURITY.md](docs/NL_DEMO_SECURITY.md).

### Portable publish layout

```powershell
powershell -File scripts/publish.ps1
```

Writes `artifacts/publish/{SessionHost,SessionHostWeb,ModerationWeb,ModerationConsole,ConfigEditor,HotkeyDaemon,Server}`. Zip that folder for a simple portable install.

Linux headless + web operators: `bash scripts/publish-linux.sh` → `artifacts/publish-linux/`.

Docker session server: `docker compose -f docker/docker-compose.session-server.yml up --build`.

**Public demo deploy (Phase F):** Caddy TLS + persistent volume — see [docs/NL_DEPLOY.md](docs/NL_DEPLOY.md). Quick start: copy `docker/.env.demo.example` → `docker/.env`, set domain + secrets, run `bash scripts/deploy-demo.sh`.

**Hosted demo loop (Phase G):** the demo compose stack auto-starts a live session and loops sample game events — see [docs/NL_DEMO.md](docs/NL_DEMO.md). No manual bridge wiring required.

**Spectator landing (Phase H):** `/` is a public read-only demo page with live decisions and try-a-rule buttons; operators use `/operator.html` — see [docs/NL_SPECTATOR.md](docs/NL_SPECTATOR.md).

**Demo hardening (Phase K):** rate limits, WebSocket caps, ops probes, and operator runbook — see [docs/NL_HARDENING.md](docs/NL_HARDENING.md) and [docs/NL_DEMO_RUNBOOK.md](docs/NL_DEMO_RUNBOOK.md).

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
.nle config  →  Lexer / Parser  →  RuleEngine  ←  NL Server (control plane)
                                      ↑
     Minecraft log | NDJSON | hotkeys | Fork runtime | mock events
                                      ↓
                         Allow / Block / Warn
                                      ↓
              RCON | process | UDP | tray action | fork action | console
                                      ↑
                         NL Fork (data plane) — licensed snapshot + server mods
```

- **NL Server** owns admission, standing, ownership, social gate, and the session bus.
- **NL Fork** owns the game snapshot instance; it emits events and receives actions through the same integration contract.
- **Same engine** for simulator, daemon, NLServer, and fork runtime — configs are not siloed per app.
- **Game adapters** only produce `GameEvent`s (name + properties); they do not reimplement rules.
- **Anti-cheat** is meant to sit on the session path (player input → check → NL Server → in-game). Today that is an early anomaly signal layer; the target is NLE-driven enforcement of gameplay/network signatures. See below.
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
| `src/NL.SessionHost.Web` | Cross-platform session bus + web dashboard (operator, join gate, fork catalog) |
| `src/NL.Moderation.Web` | Cross-platform moderation console (web) |
| `src/NL.Identity` (+ `.Core`) | Platform identity, ownership verification (Phase L) |
| `src/NL.Social` (+ `.Core`) | Live social gate, follow/sub cache (Phase M) |
| `src/NL.Fork.Core` | Fork runtime, server-side mods, hello-fork |
| `src/NL.Fork.Catalog` (+ `.Core`) | Major-version snapshot registry (Phase N) |
| `src/NL.Fork.Orchestrator` (+ `.Core`) | Ephemeral fork provisioning per session (Phase O) |
| `src/NL.Partnership` (+ `.Core`) | Publisher/platform legal gate, SDK spec, ban sync (Phase Q) |
| `src/NL.Client` (+ `.Core`) | Cross-platform join shell, deep links, overlay (Phase R) |
| `src/NL.Fleet` (+ `.Core`) | Multi-region fleet ops, relay, observability, SLOs (Phase S) |
| `src/NL.Fork.Runtime` | Fork runtime CLI |
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
| [`docs/NL_COMPLETE_GUIDE.md`](docs/NL_COMPLETE_GUIDE.md) | **Full install & run guide** — download, prerequisites, every path (simulator → public demo) |
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
| [`docs/NL_DEPLOY.md`](docs/NL_DEPLOY.md) | CI/CD + public demo deploy (Phase F) |
| [`docs/NL_DEMO.md`](docs/NL_DEMO.md) | Hosted demo loop — auto session + bridge (Phase G) |
| [`docs/NL_SPECTATOR.md`](docs/NL_SPECTATOR.md) | Spectator vs operator UX (Phase H) |
| [`docs/NL_HARDENING.md`](docs/NL_HARDENING.md) | Demo hardening & ops (Phase K) |
| [`docs/NL_DEMO_RUNBOOK.md`](docs/NL_DEMO_RUNBOOK.md) | Operator runbook (deploy, reset, monitor) |
| [`docs/NL_FORK_PLATFORM.md`](docs/NL_FORK_PLATFORM.md) | Fork platform architecture (Server vs Fork) |
| [`docs/NL_FORK_CATALOG.md`](docs/NL_FORK_CATALOG.md) | Snapshot registry, partnership tiers (Phase N) |
| [`docs/NL_FORK_ORCHESTRATOR.md`](docs/NL_FORK_ORCHESTRATOR.md) | Ephemeral fork provisioning (Phase O) |
| [`docs/NL_PARTNERSHIP.md`](docs/NL_PARTNERSHIP.md) | Publisher partnerships, at-own-risk gate (Phase Q) |
| [`docs/NL_CLIENT.md`](docs/NL_CLIENT.md) | NL Client join shell, deep links (Phase R) |
| [`docs/NL_FLEET_OPS.md`](docs/NL_FLEET_OPS.md) | Fleet ops, relay, observability, SLOs (Phase S) |
| [`docs/NL_FORK_RUNTIME.md`](docs/NL_FORK_RUNTIME.md) | Fork runtime + hello-fork (Phase P) |
| [`docs/NL_IDENTITY.md`](docs/NL_IDENTITY.md) | Platform identity & ownership (Phase L) |
| [`docs/NL_SOCIAL_GATE.md`](docs/NL_SOCIAL_GATE.md) | Live social gate & join policy (Phase M) |
| [`docs/NL_INTEGRATION_SPEC.md`](docs/NL_INTEGRATION_SPEC.md) | Universal game integration contract |

---

## License

MIT — see [LICENSE](LICENSE).
