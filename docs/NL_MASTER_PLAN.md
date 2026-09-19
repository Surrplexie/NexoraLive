# NexoraLive — Master Plan (Phases, Tracks & Roadmap)

**Purpose:** Single entry point for **everything** NexoraLive is building — core platform phases,
fork platform, production deploy ladder, dogfood tracks, game expansion (T0–T8), and long-term
vision. Use this doc to orient; use linked docs for implementation detail.

**Audience:** Operators, contributors, and future-you picking up work.

**Status legend:** ✅ built / validated in repo · 🟡 partial or operator-deploy pending · ❌ not started

**Last updated:** 2026-09-19 — Path A live; session 1 join Completed. Next: session 2. See `docs/PATH_A_PROOF.md`.

---

## Table of contents

1. [North star](#1-north-star)
2. [Phase naming — read this first](#2-phase-naming--read-this-first)
3. [Architecture at a glance](#3-architecture-at-a-glance)
4. [Track A — Core platform (Phases 0–5)](#4-track-a--core-platform-phases-05)
5. [Track B — Session server & demo (Phases A–K, I)](#5-track-b--session-server--demo-phases-ak-i)
6. [Track C — Fork platform (Phases L–S)](#6-track-c--fork-platform-phases-ls)
7. [Track D — Production deploy ladder (Phases P3–P14)](#7-track-d--production-deploy-ladder-phases-p3p14)
8. [Track E — Dogfood & validation](#8-track-e--dogfood--validation)
9. [Track F — Game expansion (Phases T0–T8)](#9-track-f--game-expansion-phases-t0t8)
10. [Track G — Economy & community (Phase 6+)](#10-track-g--economy--community-phase-6)
11. [Dependency graph (all tracks)](#11-dependency-graph-all-tracks)
12. [What is built today](#12-what-is-built-today)
13. [Recommended next work](#13-recommended-next-work)
14. [Timelines](#14-timelines)
15. [Per-game onboarding checklist](#15-per-game-onboarding-checklist)
16. [Document index](#16-document-index)
17. [Quick commands](#17-quick-commands)

---

## 1. North star

**One live stream session = one NL-governed world.**

| Principle | Meaning |
|-----------|---------|
| **NL hosts** | Authoritative game instance on NL infrastructure (fork or modded dedicated), not a random viewer PC |
| **Native client** | Players use the normal licensed game on PC / console; NL does not replace the game executable |
| **NL app** | Account, admit, social gate, moderation, launch/connect params — not gameplay netcode inside the app |
| **Join via NL only** | No stray Steam/platform invites to session endpoints |
| **Ephemeral** | World/save discarded when stream ends; **no progress transfer** to publisher cloud |
| **Streamer rules** | `.nle` RuleEngine applies Allow / Block / Warn inside the session |
| **Cross-platform vision** | NL app on PC, PlayStation, Xbox, Switch — subject to platform partnership (T6) |

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

**Ideal NL path for native SP titles** (RimWorld, Kenshi, Witcher 3, etc.): NL hosts server,
players use native client, NL app on PC/console handles admit. Not all titles are equally feasible —
see [Game taxonomy](#game-taxonomy) and [Track F](#9-track-f--game-expansion-phases-t0t8).

---

## 2. Phase naming — read this first

The repo uses **three parallel phase numbering systems**. They are intentional; do not merge them.

| System | Range | Where documented | What it covers |
|--------|-------|------------------|----------------|
| **Core platform** | 0, 0.5–0.7, 1–5 | [ROADMAP.md](../ROADMAP.md) | Rules engine, hotkeys, SP model, NLServer, moderation, anti-cheat |
| **Session / demo** | A–K, I | [ROADMAP.md](../ROADMAP.md) | Integration spec, session bus, Docker demo, spectator, hardening |
| **Fork platform** | L–S | [ROADMAP.md](../ROADMAP.md), [NL_FORK_PLATFORM.md](NL_FORK_PLATFORM.md) | Identity, social gate, catalog, orchestrator, runtime, client, fleet |
| **Production deploy** | **P3–P14** (this doc) | `docs/NL_*_PRODUCTION*.md` | Staging → fleet → beta → GA → live → launch — **not** the same as core Phase 3–5 |
| **Game expansion** | T0–T8 | [NL_GAME_EXPANSION_PLAN.md](NL_GAME_EXPANSION_PLAN.md) | Adding new games toward ideal NL path |
| **Long-term economy** | 6+ | [ROADMAP.md](../ROADMAP.md) | SPt, SrC, hub — high-risk, post-fork stability |

When someone says “Phase 5,” clarify: **anti-cheat** (core) vs **public beta** (production deploy).

---

## 3. Architecture at a glance

```text
┌─────────────────────────────────────────────────────────────────┐
│                    NL Control Plane (built)                      │
│  SessionHost.Web · RuleEngine · Identity · Social · Moderation   │
│  Fork Catalog · Orchestrator · NL Client shell · Fleet ops       │
└────────────────────────────┬────────────────────────────────────┘
                             │ ws://…/nl/v1  (Integration Spec v1)
         ┌───────────────────┼───────────────────┐
         ▼                   ▼                   ▼
  ┌─────────────┐    ┌─────────────┐    ┌─────────────────┐
  │ Bridge path │    │ Bridge path │    │ Fork path       │
  │ MC log/RCON │    │ BeamNG Lua  │    │ NL Docker fork  │
  │ Paper plugin│    │ NDJSON file │    │ IForkRuntime    │
  └─────────────┘    └─────────────┘    └─────────────────┘
```

| Layer | Status | Key docs |
|-------|--------|----------|
| Control plane | ✅ prototype | [NL_FORK_PLATFORM.md](NL_FORK_PLATFORM.md), [NL_SESSION_SERVER.md](NL_SESSION_SERVER.md) |
| Data plane (games) | 🟡 hello-fork, MC, BeamNG, RimWorld | [NL_FORK_GAME_IMAGES.md](NL_FORK_GAME_IMAGES.md) |
| Production stacks | ✅ local validate; 🟡 VPS operator | [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) |

**Integration paths**

- **Bridge** — streamer or NL runs game; mod/log/plugin emits events ([NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md)).
- **Fork** — NL orchestrator runs container; runtime implements propose-then-commit ([NL_FORK_RUNTIME.md](NL_FORK_RUNTIME.md)).

Bridges remain valid for self-hosted dedicated servers. **Fork path** is required for titles with no server access and is the long-term ideal for “NL hosts the world.”

---

## 4. Track A — Core platform (Phases 0–5)

Foundation: rules language, Windows tooling, SP model, real game hooks, moderation, anomaly signals.

| Phase | Name | Status | Summary |
|-------|------|--------|---------|
| **0** | NLEvents language + rule engine | ✅ | Lexer, parser, `RuleEngine`, simulator, tests |
| **0.5** | Windows hotkey daemon | ✅ | Tray app, global hotkeys, mic mute, NAudio |
| **0.6** | Daemon polish | ✅ | Unified `.nle` hotkeys, auto-reload, OBS clip |
| **0.7** | Validate & harden | ✅ | Single-instance, start-at-login, compound conditions |
| **1** | Config authoring UX | ✅ | WinForms `NL.ConfigEditor`, round-trip `.nle` |
| **2** | SP relationship + join flow | ✅ | Standing, roles, `JoinEligibilityEngine` — [SP_MODEL.md](SP_MODEL.md) |
| **3** | NLServer + game integration | ✅ | Minecraft + generic NDJSON — [NLSERVER.md](NLSERVER.md) |
| **4** | Moderation tooling | ✅ | JSONL audit, `ModerationConsole` — [MODERATION.md](MODERATION.md) |
| **5** | Anti-cheat signals | ✅ | Anomaly detectors wrapping event stream — [ANTICHEAT.md](ANTICHEAT.md) |

**Vertical slices (same track, not numbered phases):**

| Slice | Status | Doc |
|-------|--------|-----|
| Session Host + join gate (Minecraft-first) | ✅ | [MINECRAFT_LIVE.md](MINECRAFT_LIVE.md) |
| BeamNG.drive bridge | ✅ | [BEAMNG.md](BEAMNG.md), [DOGFOOD_BEAMNG.md](DOGFOOD_BEAMNG.md) |

**Detail:** [ROADMAP.md](../ROADMAP.md) § Phase 0–5.

---

## 5. Track B — Session server & demo (Phases A–K, I)

Hosted session product shape: REST admit, session bus, Docker demo, public spectator, browser editor.

| Phase | Name | Status | Summary |
|-------|------|--------|---------|
| **A** | Universal integration spec | ✅ | Protocol v1, TCP/WS transports, `integrations/` — [NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md) |
| **B** | Session bus | ✅ | `NL.SessionHost.Web`, WebSocket bridge — [NL_SESSION_BUS.md](NL_SESSION_BUS.md) |
| **C** | Cross-platform operator tooling | ✅ | Linux headless, moderation web — [NL_HEADLESS_LINUX.md](NL_HEADLESS_LINUX.md) |
| **D** | Networked session server | ✅ | Admit API, manifest — [NL_SESSION_SERVER.md](NL_SESSION_SERVER.md) |
| **E** | Demo security & secrets | ✅ | Operator key, token redaction — [NL_DEMO_SECURITY.md](NL_DEMO_SECURITY.md) |
| **F** | CI/CD & container deploy | ✅ | GitHub Actions, GHCR, Caddy — [NL_DEPLOY.md](NL_DEPLOY.md) |
| **G** | Hosted demo loop | ✅ | Auto-start, reset, demo bridge — [NL_DEMO.md](NL_DEMO.md) |
| **H** | Spectator vs operator UX | ✅ | Public watch page, rate-limited triggers — [NL_SPECTATOR.md](NL_SPECTATOR.md) |
| **I** | Web rule authoring | ✅ | `/editor.html` — [NL_EDITOR.md](NL_EDITOR.md) |
| **K** | Demo hardening & ops | ✅ | Rate limits, WS guard, runbook — [NL_HARDENING.md](NL_HARDENING.md), [NL_DEMO_RUNBOOK.md](NL_DEMO_RUNBOOK.md) |

**Detail:** [ROADMAP.md](../ROADMAP.md) § Phase A–K.

---

## 6. Track C — Fork platform (Phases L–S)

The **body** to Phase 0–K’s **brain**: licensed snapshots, ephemeral forks, ownership, server-side enforcement.

```text
L Platform auth ──► M Live SP gate ──► N Fork catalog
                                            │
                                            ▼
                  P Fork runtime ◄── O Ephemeral NLS ◄── Q Partnership tiers
                                            │
                                            ▼
                                  R NL Client shell ──► S Fleet ops
                                            │
                                            ▼
                            (bridge titles migrate into hosted forks)
                            T0–T8 game expansion (Track F)
```

| Phase | Name | Status | Summary |
|-------|------|--------|---------|
| **L** | Platform identity & ownership | ✅ | Steam/Epic/Xbox/PS OAuth, admit gate — [NL_IDENTITY.md](NL_IDENTITY.md) |
| **L+** | Account verification (email + 2FA) | ✅ | [NL_ACCOUNT_VERIFICATION.md](NL_ACCOUNT_VERIFICATION.md) |
| **L+** | **Unified NL login** | ✅ | One account = identity + SP + streamer — [NL_UNIFIED_LOGIN.md](NL_UNIFIED_LOGIN.md) |
| **M** | Live social gate | ✅ | Twitch/YouTube/Kick/Discord OAuth, live-only NLS — [NL_SOCIAL_GATE.md](NL_SOCIAL_GATE.md) |
| **M+** | **Live social dogfood** | ✅ | Mock/live fixtures, E2E flow — [NL_LIVE_SOCIAL_DOGFOOD.md](NL_LIVE_SOCIAL_DOGFOOD.md) |
| **N** | Fork catalog & snapshot registry | ✅ | `gameId@major`, tiers, mod hub — [NL_FORK_CATALOG.md](NL_FORK_CATALOG.md) |
| **O** | Ephemeral NLS provisioning | ✅ | `NlForkOrchestrator`, Docker backend — [NL_FORK_ORCHESTRATOR.md](NL_FORK_ORCHESTRATOR.md) |
| **P** | Fork runtime & enforcement | ✅ | `IForkRuntime`, hello-fork + MC + BeamNG + RimWorld images — [NL_FORK_RUNTIME.md](NL_FORK_RUNTIME.md) |
| **Q** | Publisher partnerships | ✅ | Official / AtOwnRisk tiers — [NL_PARTNERSHIP.md](NL_PARTNERSHIP.md) |
| **R** | NL Client shell | ✅ | Join flow, deep links — [NL_CLIENT.md](NL_CLIENT.md) |
| **S** | Fleet operations & scale | ✅ | Relay, autoscale, 100+ session validation — [NL_FLEET_OPS.md](NL_FLEET_OPS.md), [NL_FLEET_STAGING.md](NL_FLEET_STAGING.md) |

### Catalog games today

| gameId | Image | Tier path |
|--------|-------|-----------|
| `hello-fork` | `nl-fork-hello` | Demo / dogfood |
| `minecraft` | `nl-fork-minecraft`, `nl-fork-minecraft-paper` | AtOwnRisk / Platform |
| `minecraft-paper` | Paper plugin path | Same |
| `beamng` | `nl-fork-beamng` | AtOwnRisk |
| `rimworld` | `nl-fork-rimworld` | AtOwnRisk (T1 **full**) — Steam 294100 · Harmony + connect :25555 |
| `kenshi` | `nl-fork-kenshi` | AtOwnRisk (T1 **full**) — Steam 233860 · community MP + connect :23386 |
| `example-game` | `nl-fork-example-game` | T0 template reference |

**Next titles:** Cities: Skylines (finish T1 three-title exit); Cyberpunk / Witcher 3 need T5/T7.

### Bridge migration (end state)

| Title style | Self-hosted bridge | NL-hosted fork |
|-------------|-------------------|----------------|
| Open dedicated server (Minecraft, CS2 community) | ✅ now | Optional |
| Moddable + local server (BeamNG, RimWorld) | ✅ now | ✅ sidecar fork |
| Closed AAA (GTA, Fortnite BR) | Not realistic | Phase Q / T5 only |

**Detail:** [ROADMAP.md](../ROADMAP.md) § Fork platform L–S.

---

## 7. Track D — Production deploy ladder (Phases P3–P14)

Sequential **operator** milestones from local staging to public GA. Prefix **P** avoids collision with core Phases 3–5.

| Phase | Name | Status | Doc | Exit criteria (summary) |
|-------|------|--------|-----|------------------------|
| **P3** | Hosted staging | ✅ local | [NL_STAGING_HOSTED.md](NL_STAGING_HOSTED.md) | HTTPS edge, relay stub, 100-session gate |
| **P4** | Production fleet | ✅ local | [NL_PRODUCTION_FLEET.md](NL_PRODUCTION_FLEET.md) | Real Docker forks, `NL_FLEET_PRODUCTION_READY` |
| **P5** | Public beta | ✅ local | [NL_PUBLIC_BETA.md](NL_PUBLIC_BETA.md) | Waitlist, live Steam, allowlist |
| **P6** | General availability | ✅ local | [NL_GENERAL_AVAILABILITY.md](NL_GENERAL_AVAILABILITY.md) | Open signup, multi-game catalog, SLA |
| **P7** | Live production | ✅ local | [NL_LIVE_PRODUCTION.md](NL_LIVE_PRODUCTION.md) | Live Steam, HTTPS, relay/TURN templates |
| **P8** | Multi-game production | ✅ local | [NL_MULTI_GAME_PRODUCTION.md](NL_MULTI_GAME_PRODUCTION.md) | hello-fork + MC + BeamNG dogfood |
| **P9** | Launch ops & trust | ✅ local | [NL_LAUNCH_OPS.md](NL_LAUNCH_OPS.md) | Status page, backups, abuse hardening |
| **P10** | Production cutover | ✅ local | [NL_PRODUCTION_CUTOVER.md](NL_PRODUCTION_CUTOVER.md) | Cutover checklist, rollback |
| **P11** | Distribution | ✅ local | [NL_DISTRIBUTION.md](NL_DISTRIBUTION.md) | Download page, streamer onboarding |
| **P12** | Scale & reliability | ✅ local | [NL_SCALE_RELIABILITY.md](NL_SCALE_RELIABILITY.md) | DR, chaos, SLO dashboards |
| **P13** | Legal & compliance | ✅ local | [NL_LEGAL_COMPLIANCE.md](NL_LEGAL_COMPLIANCE.md) | Terms, privacy, retention |
| **P14** | Public GA launch | ✅ local | [NL_PUBLIC_GA_LAUNCH.md](NL_PUBLIC_GA_LAUNCH.md) | Operator signoff, launch checklist |
| **P14+** | Production dogfood | ✅ local | [NL_PRODUCTION_DOGFOOD.md](NL_PRODUCTION_DOGFOOD.md) | Full onboarding + Docker fork E2E |

**Operator gap (all P tracks):** 🟡 VPS with real domain + live Steam — [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md).

```text
P3 Staging → P4 Fleet → P5 Beta → P6 GA → P7 Live → P8 Multi-game
    → P9 Launch ops → P10 Cutover → P11 Distribution → P12 Scale
    → P13 Legal → P14 GA launch → Production dogfood
```

---

## 8. Track E — Dogfood & validation

Parallel validation paths; not sequential phases but required before trusting production.

| Track | Purpose | Status | Doc / scripts |
|-------|---------|--------|---------------|
| **General dogfood** | Setup → start → join → teardown | ✅ | [NL_DOGFOOD_FLOW.md](NL_DOGFOOD_FLOW.md), `scripts/nl-dogfood-flow.ps1` |
| **Unified login validate** | Register/login/session/me | ✅ | [NL_UNIFIED_LOGIN.md](NL_UNIFIED_LOGIN.md), `scripts/nl-unified-login-validate.ps1` |
| **Live social dogfood** | Mock/live social gate matrix | ✅ | [NL_LIVE_SOCIAL_DOGFOOD.md](NL_LIVE_SOCIAL_DOGFOOD.md), `scripts/nl-live-social-dogfood-*.ps1` |
| **Production dogfood** | GA stack + Docker forks + identity | ✅ local | [NL_PRODUCTION_DOGFOOD.md](NL_PRODUCTION_DOGFOOD.md) |
| **Fleet staging** | 100+ concurrent sessions | ✅ | [NL_FLEET_STAGING.md](NL_FLEET_STAGING.md), `scripts/nl-fleet-staging-validation.ps1` |
| **Game adapter validate** | Per-game onboarding gate | ✅ T0 | `scripts/nl-game-adapter-validate.ps1`, [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md) |

### Live social dogfood (recent)

| Component | Location |
|-----------|----------|
| Mock/live fixtures | `samples/social/dogfood-*.json` |
| API | `POST /api/v1/dogfood/setup` with `{ socialMode: "mock"|"live" }` |
| Operator UI | `/live-social-dogfood.html` |
| Launcher | `scripts/nl-session-host-live-social-dogfood.ps1` |

**Known operator notes:** set `NL_FLEET_MIN_TWITCH_FOLLOWERS=0` for local dogfood; use Debug `dotnet run` (not stale Release `--no-build`).

### Unified NL login (recent)

| API | Purpose |
|-----|---------|
| `POST /api/v1/auth/register\|login\|logout` | Session cookies / Bearer |
| `GET /api/v1/auth/me` | Current account |
| `POST /api/v1/auth/streamer/enable` | Promote to streamer |
| UI | `/login.html`, `nl-auth.js`, `login-app.js` |

One NL account ID flows through identity admit, SP profile, and streamer session start.

---

## 9. Track F — Game expansion (Phases T0–T8)

Extends fork platform **after Phase S** toward the ideal NL path for **many more titles**.

**Full detail:** [NL_GAME_EXPANSION_PLAN.md](NL_GAME_EXPANSION_PLAN.md)

### Phase overview

| Phase | Name | Est. duration | Unlocks |
|-------|------|---------------|---------|
| **T0** | Integration contract | ✅ | Standard per-game onboarding — [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md) |
| **T1** | Mod-backed MP titles | 🟡 | RimWorld ✅ full — [NL_RIMWORLD.md](NL_RIMWORLD.md); Kenshi ✅ full — [NL_KENSHI.md](NL_KENSHI.md); Skylines next |
| **T2** | Connect & relay production | 🟡 | **T2-lite ✅** public RimWorld URI — [NL_T2_LITE.md](NL_T2_LITE.md); full relay still open |
| **T3** | Desktop NL Client app | ❌ | Native PC install path |
| **T4** | Fragile / co-op mod titles | ❌ | Fallout NV, Subnautica, experimental tier |
| **T5** | Publisher partnership pipeline | ❌ | Official tier, closed titles |
| **T6** | Console identity & join | ❌ | Xbox/PS admit + connect where allowed |
| **T7** | NL Sync R&D | ❌ | True SP shared worlds (optional) |
| **T8** | Scale, economy, hub | ❌ | SPt, mod hub (extends Phase 6+) |

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

### Game taxonomy

| Title style | NL path | Examples |
|-------------|---------|----------|
| Open dedicated server | Bridge and/or NL fork | Minecraft ✅ |
| Moddable + local/host server | Bridge mod and/or sidecar fork | BeamNG ✅, RimWorld, Kenshi |
| SP-only or closed netcode | Publisher fork (T5) or netcode R&D (T7) | Hollow Knight, GoW, Witcher 3 |
| Closed AAA online | Official partnership only | Fortnite-style (out of scope) |

### Example title → phase mapping

| Game | Realistic NL phase | Notes |
|------|-------------------|-------|
| Minecraft | **Done** (L–P) | Catalog + Paper |
| BeamNG.drive | **Done** | Lua mod + sidecar |
| RimWorld | **T1 ✅ full** | Harmony / Together — [NL_RIMWORLD.md](NL_RIMWORLD.md) |
| Kenshi | **T1 ✅ full** | Community MP — [NL_KENSHI.md](NL_KENSHI.md) |
| Cities: Skylines | **T1** | CSMP-style |
| Fallout: New Vegas | **T4** | Fragile community MP |
| Subnautica | **T4** | Co-op mods (Nitrox-style) |
| Cyberpunk 2077 | **T4 research / defer** | No MP foundation |
| Witcher 3 / GoW / Hollow Knight | **T5 or T7** | Publisher fork or NL Sync |
| Hogwarts Legacy | **T5 or T7** | Publisher-dependent |

**Key insight:** NL is not a generic “turn any SP game into MP” engine. Something must provide **multiplayer or an authoritative simulation** — community mod, publisher fork, or NL Sync R&D.

### T0 deliverables ✓

- [x] `IGameForkAdapter` contract (+ manifest JSON / registry / checklist)
- [x] `integrations/_template/` folder (+ `example-game` reference)
- [x] `scripts/nl-game-adapter-validate.ps1` (+ `.sh`, CI job)
- [x] Adapter checklist in CI (`GameForkAdapterTests`)
- [x] Docs — [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md)

### T1 first title ✓ RimWorld

Per-title: legal tier, Docker image, event bridge, catalog row, orchestrator profile, `.nle` template, Steam app ID **294100**, dogfood script — **met**. See [NL_RIMWORLD.md](NL_RIMWORLD.md).

### T1 second title ✓ Kenshi

Per-title: legal tier, Docker image, event bridge, catalog row, orchestrator profile, `.nle` template, Steam app ID **233860**, dogfood script — **met**. See [NL_KENSHI.md](NL_KENSHI.md).

**Remaining T1:** Cities: Skylines dogfood (three-title exit).

**Exit (Kenshi):** Streamer selects Kenshi → follower with ownership + social passes admit → steal/chat/raid rule fires via RuleEngine.

**Exit (RimWorld):** Streamer selects RimWorld → follower with ownership + social passes admit → grief/chat rule fires via RuleEngine.

### T0–T8 success metrics

| Phase | Metric |
|-------|--------|
| T0 | New game onboarded in < 2 weeks using template |
| T1 | 3 mod-backed titles pass automated dogfood |
| T2 | Cross-network join on VPS; production dogfood green |
| T3 | 80% joins via desktop app |
| T4 | 1 experimental title with honest UX banner |
| T5 | 1 Official catalog entry, publisher signed |
| T6 | Console account link + admit; 1 crossplay join demo |
| T7 | Spike complete + go/no-go published |
| T8 | Hub live; SPt pilot with legal approval |

---

## 10. Track G — Economy & community (Phase 6+)

Long-term, high-risk features from [NexoraLive.txt](../NexoraLive.txt). **Depends on Phase L** at minimum. Deliberately not scheduled until fork platform L–M and T1–T2 are stable.

| Item | Status | Notes |
|------|--------|-------|
| SPt points economy (predictions, polls) | ❌ | Non-monetary first |
| SrC blockchain trading cards + marketplace | ❌ | Legal review required |
| StreamerBids (live escrow auctions) | ❌ | Gambling-adjacent |
| Full mobile admin app | ❌ | Subset in Phase R |
| Community hub — verified NLE + mods marketplace | ❌ | Overlaps T8 |
| Cross-platform clip sync | ❌ | Twitch/YouTube ↔ NLE logs |
| VIP paid streams | ❌ | Streamer retains rights |
| NL non-profit streamer ownership governance | ❌ | Org model from nl.txt |

**Relationship:** T8 implements production-scale hub/economy after game expansion proves traffic.

---

## 11. Dependency graph (all tracks)

```text
                    ┌──────────────────────────────────────┐
                    │  Track A: Core 0–5 + vertical slices │
                    └──────────────────┬───────────────────┘
                                       ▼
                    ┌──────────────────────────────────────┐
                    │  Track B: Session server A–K       │
                    └──────────────────┬───────────────────┘
                                       ▼
                    ┌──────────────────────────────────────┐
                    │  Track C: Fork platform L–S        │
                    │  (+ unified login, live social)    │
                    └──────────────┬───────────┬───────────┘
                                   ▼           ▼
              ┌────────────────────────┐   ┌─────────────────────┐
              │ Track D: P3–P14 deploy │   │ Track F: T0–T8 games │
              └────────────┬───────────┘   └──────────┬──────────┘
                           ▼                            ▼
              ┌────────────────────────┐   ┌─────────────────────┐
              │ Track E: Dogfood       │   │ Track G: 6+ economy  │
              └────────────────────────┘   └─────────────────────┘
```

**Hard dependencies for ideal NL path on new games:**

| Need | From |
|------|------|
| Ownership at admit | L |
| Social / live-only gate | M |
| Catalog row | N |
| Docker provision | O |
| In-fork rules | P |
| At-own-risk / Official copy | Q |
| Player join UX | R |
| Public connect | S + T2 |
| Standard onboarding | T0 |

---

## 12. What is built today

### ✅ In repo and validated locally

- Full rule engine stack (Phases 0–5) + BeamNG + Minecraft paths
- Session server, demo loop, spectator, browser editor (A–K, I)
- Fork platform L–S including unified login and live social dogfood
- Catalog: hello-fork, minecraft, minecraft-paper, beamng, rimworld, example-game
- Production deploy stacks P3–P14 + production dogfood (compose + validate scripts)
- T0 adapter contract + T1 RimWorld vertical slice
- Tests: Identity (25), Social (17), Client (8), Fork, Fleet, etc.

### 🟡 Partial / operator action required

- VPS deploy on real domain with live Steam ([NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md))
- Live social dogfood E2E on operator machine (fleet env, fresh build)
- Kick live follow API (Kick platform limitation)
- Console live ownership APIs (stubs exist)
- T1 Cities: Skylines (complete three-title exit)

### ❌ Not started

- T1 Cities: Skylines
- T2 public connect / relay on VPS (beyond T2-lite)
- T3+ game expansion / economy
- Operator VPS signoff on real domain
- T1+ catalog titles (RimWorld, Kenshi, …)
- T3 native desktop app
- T5–T7 publisher / console / NL Sync
- Phase 6+ economy features

---

## 13. Recommended next work

Ordered by dependency and conversation priority:

| Priority | Work | Track |
|----------|------|-------|
| 1 | **Session 2** on `play.20062006.xyz` (then restore live ownership) — [PATH_A_PROOF.md](PATH_A_PROOF.md) | D/E |
| 2 | Set `NL_PUBLIC_BASE_URL`; optional native RimWorld connect to `:25555` | D |
| 3 | ~~**T2-lite** — public `forkConnectEndpoint` on VPS for RimWorld~~ ✅ | F |
| 4 | ~~**T1 Kenshi** — second mod-backed title~~ ✅ | F |
| 5 | **T1 Cities: Skylines** — **frozen** until session 2 exists | F |
| — | ~~T0 adapter template~~ ✅ · ~~T1 RimWorld~~ ✅ · ~~public-line code path~~ ✅ · ~~Path A session 1~~ ✅ | F/D |

**Defer** marketing/support claims for pure SP AAA titles (Hollow Knight, Witcher 3) until T5/T7.

---

## 14. Timelines

### Production deploy (operator)

| When | Milestone |
|------|-----------|
| Now | Local P3–P14 validation green |
| Next | VPS + DNS + live Steam ([NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md)) |
| Launch | P14 signoff + production dogfood on VPS |

### Game expansion (18-month sketch)

| Quarter | Focus | Milestone |
|---------|--------|-----------|
| Q1 | T0 + T1 start | Adapter template; RimWorld local dogfood |
| Q2 | T1 finish + T2 | RimWorld + Kenshi in catalog; VPS relay |
| Q3 | T3 + T1 third title | Desktop NL Client beta; Cities: Skylines |
| Q4 | T4 + T5 kickoff | One experimental title; publisher pilot LOI |
| Y2 H1 | T5 + T6 | First Official tier or console admit pilot |
| Y2 H2 | T7 decision + T8 | NL Sync go/no-go; community hub if traffic |

### First 90 days (game expansion)

| Week | Action |
|------|--------|
| 1–2 | T0: template folder, adapter interface, validate script skeleton |
| 3–6 | T1-RimWorld: MP mod, Docker, catalog, local dogfood |
| 7–8 | RimWorld: social gate + unified login in dogfood |
| 9–10 | T2-lite: VPS manifest with public connect URL ✅ |
| 11–12 | T1 Kenshi full (catalog, runtime, dogfood) |

---

## 15. Per-game onboarding checklist

Use for **every** new catalog title (T0+):

```markdown
## <gameId>@<major>

- [ ] Legal tier (AtOwnRisk / Platform / Official)
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
- [ ] Live social dogfood (optional): follower/stranger matrix
- [ ] README / NL_FORK_GAME_IMAGES.md section
```

### Repo module map

```text
Streamer picks game
  → ForkCatalogHost (N)           ← catalog row
  → Session profile + .nle        ← samples/configs/
  → Social + Identity (M/L)       ← built
  → NlForkOrchestrator (O)        ← CreateSession → Docker
  → NL.Fork.Runtime (P)           ← --game <id>
  → Manifest forkConnectEndpoint  ← NL Client
  → Player admit (R)              ← /api/v1/client/join-flow
  → Events/actions loop           ← Integration Spec v1
```

---

## 16. Document index

| Doc | Track |
|-----|-------|
| [ROADMAP.md](../ROADMAP.md) | A, B, C — checkbox source of truth |
| [NL_GAME_EXPANSION_PLAN.md](NL_GAME_EXPANSION_PLAN.md) | F — T0–T8 deep dive |
| [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md) | F — T0 adapter contract / template |
| [NL_T2_LITE.md](NL_T2_LITE.md) | F — T2-lite public RimWorld connect |
| [NL_FORK_PLATFORM.md](NL_FORK_PLATFORM.md) | C — architecture |
| [NL_UNIFIED_LOGIN.md](NL_UNIFIED_LOGIN.md) | C — auth |
| [NL_LIVE_SOCIAL_DOGFOOD.md](NL_LIVE_SOCIAL_DOGFOOD.md) | E — social dogfood |
| [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) | D — operator VPS |
| [NL_COMPLETE_GUIDE.md](NL_COMPLETE_GUIDE.md) | Install & run all paths |
| [NexoraLive.txt](../NexoraLive.txt) | Original vision paper |

---

## 17. Quick commands

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

# Build
dotnet build src/NL.SessionHost.Web

# Unified login validation
powershell -File scripts/nl-unified-login-validate.ps1

# Live social dogfood (mock)
powershell -File scripts/nl-session-host-live-social-dogfood.ps1 -SocialMode mock
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -SocialMode mock

# Fork catalog + orchestrator local
$env:NL_FORK_CATALOG_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "docker"
dotnet run --project src/NL.SessionHost.Web

# Production dogfood stack
powershell -File scripts/nl-production-dogfood-stack-up.ps1 -Validate

# VPS deploy (on server)
# See docs/NL_VPS_DEPLOY.md

# T1 Kenshi (full)
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId kenshi
powershell -File scripts/nl-dogfood-flow-kenshi.ps1 -ExpectProvisioner mock
powershell -File scripts/nl-game-adapter-validate.ps1 -GameId rimworld
powershell -File scripts/nl-t2-lite-validate.ps1
powershell -File scripts/nl-dogfood-flow-rimworld.ps1 -ExpectProvisioner mock
powershell -File scripts/nl-dogfood-flow-rimworld.ps1
```

---

## Document history

| Date | Change |
|------|--------|
| 2026-08-30 | Phase T1 Kenshi shipped — runtime, catalog, dogfood, Steam 233860 |
| 2026-08-30 | Phase T2-lite — public RimWorld `forkConnectEndpoint` on VPS |
| 2026-08-12 | Phase T1 RimWorld shipped — runtime, catalog, dogfood, Steam 294100 |
| 2026-08-12 | Phase T0 shipped — game adapter contract, template, validate CI gate |
| 2026-08-11 | Initial master plan — consolidates ROADMAP, production P3–P14, dogfood tracks, T0–T8, unified login, live social |

**Maintenance:** Update checkboxes in [ROADMAP.md](../ROADMAP.md) when implementation lands; update this doc when tracks or priorities shift. Do not duplicate checkbox state here — link to ROADMAP for granular progress.
