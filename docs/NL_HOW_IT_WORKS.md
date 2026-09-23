# How NexoraLive really works (ideal plan)

**Read this first** if you want the plain-English picture — not phase codes, not deploy scripts.

NexoraLive (NL) is **not** a new game, **not** a Steam replacement, and **not** “rules in a web page only.”  
It is a **session door + rule brain + hosted world** so a streamer’s community can play **under absolute, streamer-written rules**.

---

## One sentence

**Door (NL app or web) → ticket → join an NL / streamer fork of the game → play with the normal game client + a small mod → every important action obeys the streamer’s `.nle` rules.**

---

## The four pieces (ideal)

```text
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ 1. NL door       │     │ 2. Game mod      │     │ 3. Fork (the room)   │
│ App (preferred)  │────▶│ In the real game │────▶│ NL or streamer host  │
│ or web today     │     │ (per title)      │     │ Docker / dedicated   │
└──────────────────┘     └──────────────────┘     └──────────┬───────────┘
                                                             │
                                                             ▼
                                                  ┌──────────────────────┐
                                                  │ 4. Absolute rules    │
                                                  │ Streamer’s .nle      │
                                                  │ Allow / Block / Warn │
                                                  └──────────────────────┘
```

| # | Piece | What it is | What it is **not** |
|---|--------|------------|---------------------|
| **1** | **NL door** | Win/mac app (ideal) or web (today): account, Steam link, admit, connect string | The game itself; gameplay netcode |
| **2** | **Game mod** | Small bridge inside the title (and often an MP mod) so the `.exe` can talk to NL | Something Steam Workshop auto-installs for you (yet) |
| **3** | **Fork** | The live world for *this* stream — spun up on NL infra or a streamer-chosen host | A random public lobby / publisher matchmaking |
| **4** | **Rules** | Plain-text `.nle` on the session bus — propose → decide → commit | Honor-system chat mods only |

Without **1**, strangers can’t be gated.  
Without **2**, the Steam `.exe` can’t enforce or report.  
Without **3**, there is no shared world under NL.  
Without **4**, you only have a private server with no streamer law.

---

## Who does what

### Streamers

1. Write (or load) rules in `.nle` — e.g. block caps spam, block grief, require follow.
2. Go live in NL → pick a game from the fork catalog → **Start session**.
3. NL starts a **fork** for that stream (or attaches a bridge to an allowed host).
4. Share the NL join link / go live so fans admit through NL — **not** a raw Steam invite to the server IP.
5. When the stream ends, the fork dies. Saves/progress on that fork do **not** transfer to the publisher’s cloud.

You control the **law** of the room. NL hosts (or brokers) the **room**.

### Gamers / Super Players (SPs)

1. Own the game on Steam (or another supported store) — NL checks ownership.
2. Open the **NL door** (web today; native app later) → pick the live streamer → pass admit (follow/sub/standing/at-own-risk as configured).
3. Get a connect string (example: `rimworld://play.example.com:25555`).
4. Launch the **normal game** from Steam (`steamapps\common\…`) with the required **mod(s)** installed.
5. Connect to the fork host/port NL gave you. Play under the streamer’s rules.

You are not installing a pirate copy. You use **your** licensed client to enter an **NL-governed** session.

### Game developers / publishers

| NL asks for | Why |
|-------------|-----|
| Players already own the game | No redistribution of your binaries by NL |
| Optional partnership / adapter | Cleaner events, dedicated builds, console paths |
| Fork = major-version snapshot | Ephemeral community sessions; **no progress transfer** to your live servers |
| Server-side or mod bridge | Rules apply on the hosted instance, not by patching every buyer’s install from NL |

NL’s pitch: **community sessions with enforceable streamer rules** without turning your live economy into a free-for-all. Ideal long-term is partnership (catalog tier, legal pages, verified images). Short-term many titles run **AtOwnRisk** with a community mod + sidecar/dedicated path.

### Operators / NL infrastructure

- Run **NL Server** (control plane): rules, admit, identity, social gate, moderation, session bus.
- Run **NL Fork** (data plane): orchestrate containers / dedicated hosts per live session.
- Do **not** ship copyrighted game files in the public image — operators mount a licensed tree when a real dedicated binary is required.

---

## Example: RimWorld on a PC

| Step | Where | What |
|------|--------|------|
| Admit | Browser / future NL app | Steam64 + ownership of app `294100` → ticket |
| Client | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\` | Normal `RimWorldWin64.exe` |
| Mod | That install’s `Mods\` | Multiplayer (e.g. Together) + NL bridge when used |
| World | VPS / NL fork | Listens on public port (e.g. `25555`) |
| Rules | Session bus | Chat/grief/combat events → Allow / Block / Warn |

The website never replaces the `.exe`. The `.exe` never replaces NL admit.

---

## Ideal vs today (honest)

| Piece | Ideal | Roughly today (2026-09) |
|-------|--------|-------------------------|
| Door | Native Win / mac (and later console shells) | **Web** operator + NL Client live; Windows hotkey tray is streamer tooling, not the full join app |
| Mod | One clear install path per title | In-repo for several titles; players still install manually; depth varies by game |
| Fork | NL or streamer host, real dedicated when needed | **Orchestrator + Docker works**; many dogfoods use a **sidecar** until a licensed `/game` mount exists |
| Rules | Absolute on every meaningful action | **Engine + bus live**; full in-game coverage needs the mod/dedicated path wired |

**Progress line:** public web door + rule engine + fork spin-up are real. Native NL app + “every player has the mod and joins a real dedicated world every night” is the main remaining product gap.

Live Path A host example: `https://play.20062006.xyz` — see [PATH_A_PROOF.md](PATH_A_PROOF.md).

---

## What NL deliberately does *not* do

- Replace Steam / Epic / console storefronts  
- Ship or pirate game binaries  
- Push streamer rules onto the publisher’s official multiplayer  
- Let stray platform invites bypass the NL join gate  
- Keep fork world progress after the stream (by design)

---

## Related docs

| Doc | Use when |
|-----|----------|
| [NL_MASTER_PLAN.md](NL_MASTER_PLAN.md) | Phases, tracks, build status |
| [NL_FORK_PLATFORM.md](NL_FORK_PLATFORM.md) | Control vs data plane detail |
| [NL_RIMWORLD.md](NL_RIMWORLD.md) / [NL_RIMWORLD_DOGFOOD_LIVE.md](NL_RIMWORLD_DOGFOOD_LIVE.md) | RimWorld dogfood |
| [NL_GAME_ADAPTER.md](NL_GAME_ADAPTER.md) | Adding a new title |
| [../README.md](../README.md) | Repo entry + what ships today |
