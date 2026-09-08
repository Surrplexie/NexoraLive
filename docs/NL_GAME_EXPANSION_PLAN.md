# NL Game Expansion Plan — Phases T0–T8

**Full multi-track plan (core, fork, production, dogfood, T0–T8):**
[NL_MASTER_PLAN.md](NL_MASTER_PLAN.md)

This document is the **detailed plan** for moving NexoraLive from today’s fork-platform prototype
(Phases L–S) toward the **ideal NL path**: NL hosts governed game sessions, players use **native
clients** on their devices, the **NL app** handles identity/admit/social/rules, and streamer
`.nle` configs enforce community behavior in-world.

It covers example titles discussed for expansion — including native singleplayer games where
communities want shared worlds, casual PvP, or streamer-gated co-op — and states honestly what
the current repo supports vs what each phase must build.

**Related docs**

| Doc | Role |
|-----|------|
| [NL_FORK_PLATFORM.md](NL_FORK_PLATFORM.md) | Control plane vs data plane, session lifecycle |
| [NL_FORK_CATALOG.md](NL_FORK_CATALOG.md) | `gameId@major` registry, partnership tiers |
| [NL_FORK_ORCHESTRATOR.md](NL_FORK_ORCHESTRATOR.md) | Ephemeral fork provisioning |
| [NL_FORK_RUNTIME.md](NL_FORK_RUNTIME.md) | In-fork enforcement (Phase P) |
| [NL_FORK_GAME_IMAGES.md](NL_FORK_GAME_IMAGES.md) | Minecraft / BeamNG / hello-fork / example-game images |
| [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md) | Phase T0 adapter contract + template |
| [NL_RIMWORLD.md](NL_RIMWORLD.md) | Phase T1 RimWorld title |
| [NL_KENSHI.md](NL_KENSHI.md) | Phase T1 Kenshi title |
| [NL_T2_LITE.md](NL_T2_LITE.md) | Phase T2-lite public RimWorld connect |
| [NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md) | Bridge + fork event contract |
| [NL_IDENTITY.md](NL_IDENTITY.md) | Ownership verification (Phase L) |
| [NL_SOCIAL_GATE.md](NL_SOCIAL_GATE.md) | Follow/sub/discord, live-only NLS (Phase M) |
| [NL_CLIENT.md](NL_CLIENT.md) | Join flow, deep links (Phase R) |
| [NL_UNIFIED_LOGIN.md](NL_UNIFIED_LOGIN.md) | One NL account = identity + SP + streamer |
| [NL_LIVE_SOCIAL_DOGFOOD.md](NL_LIVE_SOCIAL_DOGFOOD.md) | Social-gated dogfood track |
| [NL_MASTER_PLAN.md](NL_MASTER_PLAN.md) | Consolidated master plan (all tracks) |
| [ROADMAP.md](../ROADMAP.md) | Built vs planned checklists (Phases 0–S, 6+) |
| [NexoraLive.txt](../NexoraLive.txt) | Original vision paper |

---

## North star — ideal NL path

**One live stream session = one NL-governed world.**

| Principle | Meaning |
|-----------|---------|
| **NL hosts** | Authoritative game instance runs on NL infrastructure (fork or modded dedicated), not on a random viewer PC |
| **Native client** | Players launch the normal licensed game on PC / console; NL does not replace the game executable |
| **NL app** | Account, admit, social gate, moderation, launch/connect params — not gameplay netcode inside the app |
| **Join via NL only** | No stray Steam/platform invites to session endpoints; [NL_SOCIAL_GATE.md](NL_SOCIAL_GATE.md) |
| **Ephemeral** | World/save discarded when stream ends; **no progress transfer** to publisher cloud |
| **Streamer rules** | `.nle` RuleEngine applies Allow / Block / Warn inside the session |
| **Cross-platform vision** | NL app on PC, PlayStation, Xbox, Switch — subject to platform partnership (Phase T6) |

```text
Streamer goes live
    → selects gameId@major from Fork Catalog
    → attaches .nle + verified server mods
    → NL verifies ownership + social gate + (optional) live-only
    → Fork Orchestrator starts ephemeral instance
    → SPs admit via NL Client / app → receive connect manifest
    → native game client connects to NL-hosted world
    → events: Fork → session bus → RuleEngine → actions back
Stream ends → fork destroyed → only .nle, moderation, metadata persist
```

---

## Baseline — what the repo supports today

Phases **0–K** (rules, simulator, Session Host) plus **L–S** (identity, social, catalog,
orchestrator, client shell, fleet) are largely implemented as prototypes. See [ROADMAP.md](../ROADMAP.md).

### Control plane (built)

| Capability | Status | Entry points |
|------------|--------|--------------|
| Join admission + standing | ✅ | `POST /api/v1/session/admit`, `JoinEligibilityEngine` |
| Platform identity (Steam mock/live, Epic/Xbox/PS OAuth routes) | ✅ / partial | [NL_IDENTITY.md](NL_IDENTITY.md) |
| Unified NL login (identity + SP + streamer) | ✅ | [NL_UNIFIED_LOGIN.md](NL_UNIFIED_LOGIN.md) |
| Live social gate (follow/sub/discord, live-only NLS) | ✅ | [NL_SOCIAL_GATE.md](NL_SOCIAL_GATE.md) |
| Moderation + offense archive | ✅ | Moderation console, JSONL audit |
| NL Client join shell | ✅ prototype | `/nl-client.html`, `NL.Client` |
| Partnership / at-own-risk gate | ✅ prototype | Phase Q |

### Data plane (partial)

| Capability | Status | Catalog / images |
|------------|--------|------------------|
| Fork catalog (`gameId@major`, tiers, mod hub) | ✅ | `samples/fork/catalog.json` |
| Fork orchestrator (mock / process / Docker) | ✅ | Phase O |
| In-fork rule enforcement | ✅ demo titles | Phase P |
| **hello-fork** | ✅ | `nl-fork-hello:latest` |
| **Minecraft Java** (sidecar + Paper plugin) | ✅ | `nl-fork-minecraft`, `nl-fork-minecraft-paper` |
| **BeamNG.drive** (sidecar + host Lua mod) | ✅ | `nl-fork-beamng:latest` |
| **User-requested titles** (Kenshi, Cyberpunk, etc.) | 🟡 RimWorld ✅ · Kenshi ✅ in catalog | Skylines next |

### Integration paths (both supported in design)

```text
                    ┌─────────────────────────────────────┐
                    │         NL Control Plane            │
                    │  SessionHost.Web · RuleEngine · …   │
                    └──────────────┬──────────────────────┘
                                   │ ws /nl/v1
              ┌────────────────────┼────────────────────┐
              │                    │                    │
              ▼                    ▼                    ▼
     ┌────────────────┐  ┌────────────────┐  ┌────────────────┐
     │ Bridge path    │  │ Bridge path    │  │ Fork path      │
     │ Minecraft log  │  │ BeamNG NDJSON  │  │ NL-hosted      │
     │ + Paper plugin │  │ + Lua mod      │  │ Docker fork    │
     └────────────────┘  └────────────────┘  └────────────────┘
```

- **Bridge path** — streamer or NL runs the game; mod/log/plugin emits events to NL ([NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md)).
- **Fork path** — NL orchestrator runs a containerized instance; runtime implements propose-then-commit ([NL_FORK_RUNTIME.md](NL_FORK_RUNTIME.md)).

Bridges remain for self-hosted dedicated servers. The **fork path** is required for titles with
no server access (most AAA) and is the long-term ideal for “NL hosts the world.”

### Cross-platform gap

| Platform | Today | Gap |
|----------|-------|-----|
| PC (Steam/Epic) | Join flow + ownership mock/live | Native desktop app (T3) |
| Xbox / PlayStation | OAuth route stubs | Live ownership APIs, store app, connect policy (T6) |
| Switch | Not started | Platform partnership |

Open question from [NL_FORK_PLATFORM.md](NL_FORK_PLATFORM.md): *Console limited without first-party SDK partnership.*

---

## Game taxonomy — example titles

Not all games are equally feasible. NL’s [ROADMAP bridge migration note](../ROADMAP.md) applies:

| Title style | NL path | Example games |
|-------------|---------|---------------|
| Open dedicated server | Bridge and/or NL fork | Minecraft (✅ in repo) |
| Moddable + local/host server | Bridge mod and/or sidecar fork | BeamNG (✅), RimWorld, Kenshi |
| SP-only or closed netcode | Publisher fork (T5) or netcode R&D (T7) | Hollow Knight, GoW 2018, Witcher 3 |
| Closed AAA online | Official partnership only | Fortnite-style (out of scope here) |

### Example title → phase mapping

| Game | Native MP today? | Realistic NL phase | Notes |
|------|------------------|-------------------|-------|
| **Minecraft** | Yes (dedicated) | **Done** (L–P) | Catalog + Paper plugin |
| **BeamNG.drive** | Freeroam / BeamMP | **Done** (bridge + sidecar) | Lua mod + optional MP |
| **RimWorld** | Community MP mods | **T1 ✅ full** | Harmony + Together dedicated / sidecar |
| **Kenshi** | Community MP mods | **T1** | Mod stack + NL host |
| **Cities: Skylines** | MP mods (e.g. CSMP) | **T1** | Modded dedicated |
| **Fallout: New Vegas** | Fragile community MP | **T4** | Experimental tier |
| **Subnautica** | Co-op mods (e.g. Nitrox) | **T4** | Stability-dependent |
| **Cyberpunk 2077** | No real MP | **T4 research / defer** | Needs netcode foundation |
| **The Witcher 3** | SP only | **T5 or T7** | Publisher fork or NL Sync |
| **God of War (2018)** | SP only | **T5 or T7** | Same |
| **Hollow Knight** | SP only | **T7 or out of scope** | No server to host |
| **Hogwarts Legacy** | SP / limited co-op | **T5 or T7** | Publisher-dependent |
| **City Skylines II** | Varies | **T1/T4** | Follow mod maturity |

**Key insight:** NL is not a generic “turn any SP game into MP” engine. Something must provide
**multiplayer or an authoritative simulation** — community mod, publisher fork, or NL Sync R&D.

---

## Phase overview (T0–T8)

Phases **T0–T8** extend the fork platform after **Phase S**. **T0 is built**; T1–T8 remain
planned. See [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md).

| Phase | Name | Duration (est.) | Unlocks |
|-------|------|-----------------|--------|
| **T0** | Integration contract | 1–2 months | Standard per-game onboarding ✅ |
| **T1** | Mod-backed MP titles | 3–4 months | RimWorld ✅ · Kenshi ✅ · Skylines planned |
| **T2** | Connect & relay production | 2–3 months | **T2-lite ✅** · full T2 planned |
| **T3** | Desktop NL Client app | 2–3 months | PC app install path |
| **T4** | Fragile / co-op mod titles | 3–5 months | Fallout NV, Subnautica, experimental tier |
| **T5** | Publisher partnership pipeline | 6–12 months | Official tier, closed titles |
| **T6** | Console identity & join | 6–12 months | Xbox/PS admit + connect where allowed |
| **T7** | NL Sync research | 12–24+ months | True SP shared worlds (optional) |
| **T8** | Scale, economy, community hub | After T1–T2 stable | SPt, mod hub, clips (ROADMAP 6+) |

### Dependency graph

```text
T0 (adapter contract)
 └─► T1 (RimWorld / Kenshi / Skylines)
      └─► T2 (relay / VPS connect)
           ├─► T3 (desktop NL Client app)
           ├─► T4 (fragile mod titles)
           └─► T5 (publisher pipeline) ──► T6 (console join)
T7 (NL Sync R&D) ── parallel; strategic go/no-go
T8 (economy / hub) ── after T1–T2 production traffic
```

---

## Phase T0 — Integration contract ✓

**Goal:** One standard way to add any game so work is not bespoke every time.

**Problem today:** Minecraft, BeamNG, and hello-fork each wired manually (`ForkGameProfiles`,
Dockerfiles, runtime `--game` flags). Adding RimWorld would touch many places without a checklist.

### Deliverables

- [x] **`IGameForkAdapter`** — documented contract in `src/NL.Fork.Core/`:
  - Required session events (map to [NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md))
  - Required actions (kick, warn, tell, …)
  - Connect URL scheme (`rimworld://host:port`, `minecraft://…`)
  - Health / readiness probe for orchestrator
- [x] **Adapter checklist** (markdown + CI gate):
  1. Catalog row in `samples/fork/catalog.json`
  2. Docker image + `docker build` in `scripts/build-fork-images.ps1`
  3. Runtime profile / plugin in `integrations/<game>/`
  4. `.nle` template in `samples/configs/<game>.nle`
  5. Dogfood script `scripts/nl-dogfood-flow-<game>.ps1` (or shared flow)
  6. Smoke / unit coverage in `tests/NL.Fork.Core.Tests/GameForkAdapterTests.cs`
- [x] **Template folder:** `integrations/_template/` (Dockerfile, sidecar stub, sample catalog snippet)
- [x] **Filled reference:** `integrations/example-game/`
- [x] **Unified validation:** `scripts/nl-game-adapter-validate.ps1` / `.sh`
- [x] **Scaffold:** `scripts/nl-game-adapter-scaffold.ps1`
- [x] **Docs:** [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md)

### Exit criteria

A new game can be onboarded by copying the template in **&lt; 2 weeks** without modifying
`Program.cs` or core SessionHost logic. **Met** via manifest discovery +
`ForkGameProfiles.Resolve` fallback to `integrations/*/adapter.manifest.json`.

### Repo touchpoints

| Area | Files / modules |
|------|-----------------|
| Runtime | `src/NL.Fork.Runtime`, `src/NL.Fork.Core` (`IGameForkAdapter`, checklist) |
| Catalog | `samples/fork/catalog.json`, `NL.Fork.Catalog` |
| Orchestrator | `src/NL.Fork.Orchestrator`, `ForkGameProfiles` |
| Template | `integrations/_template/`, `integrations/example-game/` |
| Docs | This file, `NL_GAME_ADAPTER.md`, `NL_FORK_GAME_IMAGES.md` |

---

## Phase T1 — Mod-backed multiplayer titles 🟡

**Goal:** Prove the ideal NL path on PC Steam titles that **already have community multiplayer**
but are not yet in the catalog.

### Priority order

1. **RimWorld** ✅ — shipped (see [NL_RIMWORLD.md](NL_RIMWORLD.md))
2. **Kenshi** ✅ — shipped (see [NL_KENSHI.md](NL_KENSHI.md))
3. **Cities: Skylines** — CSMP or equivalent

### Per-title work breakdown (RimWorld ✓)

| Step | Description | RimWorld |
|------|-------------|----------|
| **Legal** | Catalog tier **AtOwnRisk** | ✅ |
| **Fork image** | Docker sidecar + NL vocabulary | ✅ `nl-fork-rimworld` |
| **Event bridge** | Integration Spec v1 events | ✅ `RimWorldForkRuntime` + Harmony mod |
| **Catalog** | `rimworld@1.0` | ✅ |
| **Orchestrator** | `ForkGameProfiles` + connect scheme | ✅ `rimworld://:25555` TCP banner |
| **Rules** | `samples/configs/rimworld.nle` | ✅ chat + grief |
| **Identity** | Steam app **294100** | ✅ mock matrix |
| **Social dogfood** | `-GameId rimworld` | ✅ |
| **Automated dogfood** | `nl-dogfood-flow-rimworld.ps1` | ✅ |

### Per-title work breakdown (Kenshi ✓)

| Step | Description | Kenshi |
|------|-------------|--------|
| **Legal** | Catalog tier **AtOwnRisk** | ✅ |
| **Fork image** | Docker sidecar + NL vocabulary | ✅ `nl-fork-kenshi` |
| **Event bridge** | Integration Spec v1 events | ✅ `KenshiForkRuntime` + hook catalog |
| **Catalog** | `kenshi@1.0` | ✅ |
| **Orchestrator** | `ForkGameProfiles` + connect scheme | ✅ `kenshi://:23386` TCP banner |
| **Rules** | `samples/configs/kenshi.nle` | ✅ chat + steal / raid |
| **Identity** | Steam app **233860** | ✅ mock matrix |
| **Social dogfood** | `-GameId kenshi` | ✅ |
| **Automated dogfood** | `nl-dogfood-flow-kenshi.ps1` | ✅ |

### Exit criteria

- [x] Streamer can select **RimWorld** from catalog / dogfood setup.
- [x] Follower with Steam ownership (294100) + social requirements can pass admit path.
- [x] At least **one** in-game rule fires (caps chat / buildingDestroy) via RuleEngine.
- [x] Streamer can select **Kenshi** from catalog / dogfood setup.
- [x] Follower with Steam ownership (233860) + social requirements can pass admit path.
- [x] At least **one** Kenshi in-game rule fires (caps chat / steal) via RuleEngine.
- [ ] **Three** mod-backed titles pass automated dogfood (RimWorld ✅ + Kenshi ✅ + Skylines).

### Ideal path progress after T1 RimWorld

| Ideal element | Status |
|---------------|--------|
| NL hosts world | ✅ (sidecar fork image) |
| Native client | ✅ |
| NL app admit (browser) | ✅ |
| Cross-platform | ❌ (PC Steam only) |
| True SP without mod | ❌ |

---

## Phase T2 — Connect & relay production 🟡

**Goal:** Players join from outside localhost; manifests work on VPS and real domains.

### T2-lite (shipped)

Public native connect for RimWorld on a VPS. Detail: [NL_T2_LITE.md](NL_T2_LITE.md).

- [x] **Public `forkConnectEndpoint`** — `rimworld://public-host:25555` (no loopback when `NL_FORK_PUBLIC_CONNECT_HOST` is set)
- [x] **Do not WSS-mask native TCP** — RimWorld / Minecraft stay `scheme://host:port`
- [x] **NL Client launch params** — `nativeConnectClipboard` + web hint
- [x] **VPS firewall 25555/tcp** + env in `vps-production.env.example`
- [x] **`GET /api/v1/t2-lite/status`** + `scripts/nl-t2-lite-validate.ps1`

**T2-lite exit:** Manifest on VPS uses the play hostname; viewer pastes URI into Together. One RimWorld session per host port.

### Full T2 (remaining)

- [ ] **Relay / TURN for game ports** — extend Phase S templates beyond control-plane WSS
- [ ] **Edge TLS** — Caddy already fronts HTTP; game TCP stays raw (or future relay)
- [ ] **Regional placement** — streamer session → fleet region → fork on VPS (placement exists; cross-region dogfood open)
- [ ] **Unique host ports** — more than one RimWorld fork per IP
- [ ] **Production dogfood** — extend [NL_PRODUCTION_DOGFOOD.md](NL_PRODUCTION_DOGFOOD.md) for T1 titles on a live domain
- [ ] **Observability** — fork create/destroy, admit counts, join latency (Phase S metrics exist; T2 join-latency SLO open)

### Exit criteria (full T2)

- Dogfood on VPS: streamer in region A, viewer in region B, join succeeds via public URL.
- [NL_PRODUCTION_DOGFOOD.md](NL_PRODUCTION_DOGFOOD.md) validation passes with at least one T1 game.

---

## Phase T3 — Desktop NL Client app

**Goal:** “Download app per device” on **PC first** (vision slice).

### Deliverables

- [ ] **Native shell** (Electron, Tauri, or .NET MAUI) — unified login, streamer list, join flow
- [ ] **Session token** storage — reuse [NL_UNIFIED_LOGIN.md](NL_UNIFIED_LOGIN.md) Bearer flow
- [ ] **Post-admit launcher** — spawn game or show connect instructions from manifest
- [ ] **Stray invite blocker** — PC-level where feasible (Phase R extension)
- [ ] **Optional overlay** — standing, warnings (port `/nl-client.html` overlay to native)
- [ ] **Distribution** — extend [NL_DISTRIBUTION.md](NL_DISTRIBUTION.md) with signed PC package
- [ ] **Auto-update** channel (stub → production)

### Exit criteria

- **80%** of dogfood joins complete without opening a browser.
- App published to at least one PC channel (direct download or store beta).

### Out of scope for T3

Console store apps (T6), in-game rendering overlay for all titles.

---

## Phase T4 — Fragile / co-op mod titles

**Goal:** Expand catalog where MP exists but is immature or high-friction.

### Candidate titles

| Game | Approach | Risk |
|------|----------|------|
| **Fallout: New Vegas** | Community MP + NL host | Legal, mod breakage |
| **Subnautica** | Co-op mod (Nitrox-style) if stable | Mod updates |
| **Cyberpunk 2077** | **Research only** until netcode exists | No MP foundation |

### Deliverables

- [ ] Same adapter pattern as T1
- [ ] Catalog `status: Experimental` vs `Active` with UI banner
- [ ] Stricter dogfood gates before promoting to `Active`
- [ ] Legal review template per tier ([NL_LEGAL_COMPLIANCE.md](NL_LEGAL_COMPLIANCE.md))

### Exit criteria

- At least **one** experimental-tier title in catalog with documented limitations.
- Go/no-go doc for Cyberpunk (continue research vs explicit deferral).

---

## Phase T5 — Publisher partnership pipeline

**Goal:** **Official / Platform** tier for closed games without community MP mods.

### Deliverables

- [ ] **Publisher onboarding playbook** — SDK, “Play on NL” button spec, ban sync (extend Phase Q)
- [ ] **Snapshot ingestion** — publisher delivers major-version build; NL registers `gameId@major`
- [ ] **Legal workflow** — EULA, deprecation, no monetization leakage, no progress transfer
- [ ] **Official tier UI** — catalog badge, in-game/legal copy approved by publisher
- [ ] **Pilot title** — one mid-size studio game with dedicated or listen-server NL can host
- [ ] **Connect story** — publisher launcher deep link or approved IP join

### Exit criteria

- **One Official-tier** catalog entry with signed publisher agreement (not AtOwnRisk).
- Pilot dogfood with publisher QA sign-off.

### Titles addressed

God of War, Witcher 3, Hogwarts Legacy, and similar **only** via this phase (or T7).

---

## Phase T6 — Console identity & join

**Goal:** Xbox / PlayStation players participate in NL **community layer**; connect when game allows.

### Deliverables

- [ ] **Live Xbox / PS OAuth** + ownership APIs (extend Phase L routes)
- [ ] **NL app on console** — store app for login, admit, social (not in-game mod)
- [ ] **Crossplay manifest** — PC-hosted fork; console client connects if title supports it
- [ ] **Platform policy checklist** — Sony, Microsoft, Nintendo requirements
- [ ] **Switch** — assess feasibility; likely lags PS/Xbox

### Exit criteria

- Console user links platform account, passes admit, receives connect info.
- Gameplay join demonstrated for **one** crossplay-enabled title (publisher-approved).

### Limitation

Console players rarely run NL **inside** the game OS. NL app is parallel; game must support
connecting to NL-hosted session.

---

## Phase T7 — NL Sync research (optional R&D)

**Goal:** Explore **true singleplayer** shared worlds where no MP mod exists (Hollow Knight, etc.).

**This is not an extension of current repo — it is a separate R&D track.**

### Options (pick one after spike)

| Approach | Description | Feasibility |
|----------|-------------|-------------|
| **Publisher fork** | Studio ships NL-compatible build with sync | T5 dependency |
| **Replication layer** | NL-built state sync inside licensed fork | Multi-year per engine |
| **Async shared world** | Votes, ghosts, stream-driven events — not full co-presence | Different product shape |
| **Explicit deferral** | Document “not supported without publisher” | Honest scope |

### Deliverables

- [ ] 6-month technical spike with one small indie SP title under NDA
- [ ] Architecture doc: `docs/NL_SYNC_RESEARCH.md`
- [ ] Executive go/no-go

### Exit criteria

- Vertical slice **or** published decision that pure SP titles require publisher partnership (T5) only.

---

## Phase T8 — Scale, economy, community hub

**Goal:** Long-term vision from [ROADMAP Phase 6+](../ROADMAP.md) after T1–T2 carry production traffic.

### Deliverables

- [ ] **SPt** — non-monetary predictions/polls first; gambling-adjacent rules per nl.txt
- [ ] **Community hub** — verified `.nle` + server mod marketplace
- [ ] **Clip sync** — Twitch/YouTube ↔ NL moderation metadata
- [ ] **Multi-region autoscale** — Phase S production hardening
- [ ] **SrC / StreamerBids** — only after legal review (high risk)

### Depends on

T1–T2 stable production, identity/compliance tracks, legal sign-off.

---

## Per-game onboarding checklist (T0+)

Use this for **every** new catalog title:

```markdown
## <gameId>@<major>

- [ ] Legal tier assigned (AtOwnRisk / Platform / Official)
- [ ] Steam / platform app IDs in ownership matrix
- [ ] Docker image: `docker/fork-<game>/`
- [ ] Image in `scripts/build-fork-images.ps1`
- [ ] Catalog row in `samples/fork/catalog.json`
- [ ] ForkGameProfiles / orchestrator wiring
- [ ] Runtime or plugin: `integrations/<game>/`
- [ ] Event vocabulary doc (game events ↔ NL Integration Spec)
- [ ] `.nle` template: `samples/configs/<game>.nle`
- [ ] Connect URL scheme documented
- [ ] NL Client manifest field tested
- [ ] Dogfood script: `scripts/nl-dogfood-flow-<game>.ps1`
- [ ] Validation: `scripts/nl-game-adapter-validate.ps1 -GameId <game>`
- [ ] Live social dogfood (optional): follower/stranger matrix in mock-social
- [ ] README / NL_FORK_GAME_IMAGES.md section
```

---

## Repo module map (where work lands)

```text
Streamer picks game
    → ForkCatalogHost (Phase N)          ← add catalog row
    → Session profile + .nle             ← samples/configs/
    → Social gate + Identity (L/M)       ← already built
    → NlForkOrchestrator (O)               ← CreateSession → Docker
    → NL.Fork.Runtime (P)                  ← --game <id> adapter
    → Manifest forkConnectEndpoint       ← NL Client launch
    → Player admit (R)                     ← /api/v1/client/join-flow
    → Events/actions loop                ← Integration Spec v1
```

| Module | Path |
|--------|------|
| Session host / APIs | `src/NL.SessionHost.Web/Program.cs` |
| Fork catalog | `src/NL.Fork.Catalog/` |
| Orchestrator | `src/NL.Fork.Orchestrator/` |
| Runtime | `src/NL.Fork.Runtime/`, `src/NL.Fork.Core/` |
| Game integrations | `integrations/`, `docker/fork-*/` |
| NL Client | `src/NL.Client/`, `wwwroot/nl-client.html` |
| Identity / social | `src/NL.Identity/`, `src/NL.Social/` |
| Tests / dogfood | `tests/`, `scripts/nl-dogfood-flow*.ps1` |

---

## Suggested timeline (18 months)

| Quarter | Focus | Milestone |
|---------|--------|-----------|
| **Q1** | T0 + T1 start | Adapter template; RimWorld local dogfood |
| **Q2** | T1 finish + T2 | RimWorld + Kenshi in catalog; VPS relay dogfood |
| **Q3** | T3 + T1 third title | Desktop NL Client beta; Cities: Skylines |
| **Q4** | T4 + T5 kickoff | One experimental title; publisher pilot LOI |
| **Y2 H1** | T5 + T6 | First Official tier **or** console admit pilot |
| **Y2 H2** | T7 decision + T8 start | NL Sync go/no-go; community hub if traffic |

---

## First 90 days (recommended)

| Week | Action |
|------|--------|
| 1–2 | T0: `integrations/_template/`, adapter interface doc, validate script skeleton |
| 3–6 | T1-RimWorld: pick MP mod, Docker dedicated, catalog row, local dogfood |
| 7–8 | RimWorld: social gate + unified login in dogfood path |
| 9–10 | T2-lite: VPS manifest with public connect URL ✅ ([NL_T2_LITE.md](NL_T2_LITE.md)) |
| 11–12 | T1 Kenshi full (catalog, runtime, dogfood) |

**Defer** messaging for Hollow Knight / GoW / Witcher until T5/T7 — NL control plane still
delivers value (followers, gates, moderation) without shared world.

---

## Success metrics

| Phase | Metric |
|-------|--------|
| **T0** | New game onboarded in &lt; 2 weeks using template |
| **T1** | 3 mod-backed titles pass automated dogfood |
| **T2** | Cross-network join on VPS; production dogfood green |
| **T3** | 80% joins via desktop app |
| **T4** | 1 experimental title with honest UX banner |
| **T5** | 1 Official catalog entry, publisher signed |
| **T6** | Console account link + admit; 1 crossplay join demo |
| **T7** | Spike complete + go/no-go published |
| **T8** | Hub live; SPt pilot with legal approval |

---

## Risks & constraints

| Risk | Mitigation |
|------|------------|
| Mod breakage on game updates | Pin mod + game version in catalog `minClientVersion`; Experimental tier |
| Publisher denial | AtOwnRisk path; no binary redistribution without license |
| Console policy | T6 partnership-first; no sideload promises |
| SP titles without MP | Do not market as supported until T5/T7; honest catalog tiers |
| Anti-cheat (EAC/Vanguard) | Server-side NL rules only; document coexistence limits |
| Cost of hosting game servers | Phase S quotas; streamer limits; idle teardown (orchestrator) |

---

## Relationship to existing ROADMAP phases

| Existing | Relationship to T0–T8 |
|----------|---------------------|
| **L** Identity | Required for all T1+ titles (ownership at admit) |
| **M** Social gate | Required for streamer community sessions |
| **N** Fork catalog | T1+ adds rows per game |
| **O** Orchestrator | T1+ adds Docker images per game |
| **P** Fork runtime | T0 standardizes adapters; T1+ implements per game |
| **Q** Partnership | T5 extends Official tier |
| **R** NL Client | T3 native app wraps R join flow |
| **S** Fleet ops | T2 production connect |
| **6+** Economy | T8 |

T0 checkboxes live in [ROADMAP.md](../ROADMAP.md) (Phase T0 ✓). Continue with T1+.

---

## Document history

| Date | Change |
|------|--------|
| 2026-08-30 | Phase T1 Kenshi **full** — community-MP bridge, connect listener, sidecar dual-mode, outpost vocabulary |
| 2026-08-30 | Phase T2-lite — public RimWorld connect URI on VPS |
| 2026-08-14 | Phase T1 RimWorld **full** — Harmony/Together bridge, connect listener, dedicated dual-mode, complete colony vocabulary |
| 2026-08-12 | Phase T1 RimWorld — `RimWorldForkRuntime`, catalog, dogfood, Steam 294100 |
| 2026-08-12 | Phase T0 shipped — `IGameForkAdapter`, `integrations/_template/`, validate/scaffold scripts |
| 2026-08-11 | Initial plan — Phases T0–T8, example title mapping, 18-month timeline |

---

## Quick reference commands (today’s repo)

```powershell
# Catalog + orchestrator local dogfood (existing titles)
$env:NL_FORK_CATALOG_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "docker"
Copy-Item samples/fork/catalog.json "$env:LOCALAPPDATA/NL/fork-catalog/catalog.json"
dotnet run --project src/NL.SessionHost.Web

# Social-gated dogfood
powershell -File scripts/nl-session-host-live-social-dogfood.ps1 -SocialMode mock
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -SocialMode mock

# Production-style stack
powershell -File scripts/nl-production-dogfood-stack-up.ps1 -Validate
```

Future T1 validation (planned):

```powershell
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId rimworld
powershell -File scripts/nl-dogfood-flow-rimworld.ps1
```
