# NexoraLive — Complete Install & Run Guide

**Audience:** Anyone who wants to download, install, build, run, and open NexoraLive (NL) — from a five-minute simulator try to a public HTTPS demo on the internet.

**Repository:** [github.com/Surrplexie/NexoraLive](https://github.com/Surrplexie/NexoraLive)

This guide is the single walkthrough for setup. For language details see [`NLE_GUIDE.md`](../NLE_GUIDE.md). For what's built vs planned see [`ROADMAP.md`](../ROADMAP.md).

---

## Table of contents

1. [What you are installing](#1-what-you-are-installing)
2. [Pick your path](#2-pick-your-path)
3. [System requirements](#3-system-requirements)
4. [Download the code](#4-download-the-code)
5. [Install prerequisites](#5-install-prerequisites)
6. [Build and verify](#6-build-and-verify)
7. [Path A — Learn NL with no game (5 minutes)](#7-path-a--learn-nl-with-no-game-5-minutes)
8. [Path B — Windows streamer toolkit](#8-path-b--windows-streamer-toolkit)
9. [Path C — Web session server (local)](#9-path-c--web-session-server-local)
10. [Path D — Docker demo loop (local)](#10-path-d--docker-demo-loop-local)
11. [Path E — Public internet demo (VPS)](#11-path-e--public-internet-demo-vps)
12. [Path F — Live Minecraft session](#12-path-f--live-minecraft-session)
13. [Path G — Live BeamNG.drive session](#13-path-g--live-beamngdrive-session)
14. [Portable publish (zip install)](#14-portable-publish-zip-install)
15. [Ports, URLs, and what to open](#15-ports-urls-and-what-to-open)
16. [Data files and where NL stores state](#16-data-files-and-where-nl-stores-state)
17. [Environment variables cheat sheet](#17-environment-variables-cheat-sheet)
18. [Troubleshooting](#18-troubleshooting)
19. [What to read next](#19-what-to-read-next)

---

## 1. What you are installing

NexoraLive is a **streamer session rules platform**. You write plain-text `.nle` configs; a shared rule engine evaluates gameplay or hotkey events and returns **Allow**, **Block**, or **Warn**.

This repo includes:

| Layer | What it does |
|-------|----------------|
| **Rule engine** | Parses `.nle`, evaluates events (`NL.Core`) |
| **Simulators** | Mock events — no game needed |
| **Windows apps** | Hotkey daemon, config editor, session host, moderation console |
| **Web apps** | Session server dashboard, moderation console, spectator demo, browser rule editor |
| **Game bridges** | Minecraft log, generic NDJSON, BeamNG Lua mod, Python reference bridge |
| **Docker stack** | One-command local or public demo with TLS |

NL is an **early working prototype**, not a finished hosted product. It is enough to learn the model, validate configs, run local sessions, and ship a **public rules-engine demo** on the web.

---

## 2. Pick your path

Use this to decide where to start. You can do more than one path.

```text
"I just want to see rules work"     → Path A (simulator)
"I'm a Windows streamer"            → Path B (hotkeys + session host)
"I want a browser dashboard"        → Path C (web session server)
"I want Docker / no .NET on host"   → Path D (Docker demo loop)
"I want a public URL for visitors"  → Path E (VPS + Caddy TLS)
"I have Minecraft Java + RCON"      → Path F
"I have BeamNG.drive"               → Path G
"I want a zip, not dotnet run"      → Portable publish (§14)
```

**Recommended first-time order:** A → C or B → F/G only when you are ready for live side effects.

---

## 3. System requirements

### Always needed (developer / source build)

| Requirement | Version | Notes |
|-------------|---------|-------|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0+ | Build, test, and `dotnet run` |
| Git | Any recent | Clone the repository |

### Windows-only apps

These require **Windows 10/11** and the .NET 8 SDK (or a published build from §14):

- `NL.HotkeyDaemon` — global hotkeys, tray app
- `NL.ConfigEditor` — visual `.nle` editor
- `NL.SessionHost` — Start/Stop session shell
- `NL.ModerationConsole` — admin WinForms UI

### Cross-platform (Windows, Linux, macOS)

With .NET 8 SDK only:

- `NL.Simulator`, `NL.SpSimulator`, `NL.Server` CLI
- `NL.SessionHost.Web` — HTTP **27020**, WebSocket **27021**
- `NL.Moderation.Web` — HTTP **27030**
- All unit tests

### Docker paths (D, E)

| Requirement | Notes |
|-------------|-------|
| Docker Engine + Compose v2 | Linux VPS or Docker Desktop on Windows/macOS |
| Domain + DNS (Path E only) | A/AAAA record to your VPS; ports **80** and **443** open |
| `openssl` or equivalent | Generate secrets (`rand -hex 32`) |

### Optional game / integration extras

| Use case | Extra software |
|----------|----------------|
| Minecraft live | Java Minecraft/Paper server, RCON enabled in `server.properties` |
| BeamNG live | BeamNG.drive (Steam), bundled Lua bridge mod |
| Python demo bridge | Python 3 + `pip install websocket-client` |
| OBS clip hotkey | OBS 28+ with WebSocket server enabled |
| BeamMP kicks | BeamMP server + `scripts/install-beammp-nl-kick.ps1` |

---

## 4. Download the code

### Option 1 — Git clone (recommended)

```bash
git clone https://github.com/Surrplexie/NexoraLive.git
cd NexoraLive
```

### Option 2 — Download ZIP

1. Open [github.com/Surrplexie/NexoraLive](https://github.com/Surrplexie/NexoraLive)
2. **Code → Download ZIP**
3. Extract and open a terminal in the extracted folder

All commands below assume your **current directory is the repo root** (the folder containing `src/`, `docs/`, `samples/`).

---

## 5. Install prerequisites

### Windows

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).
2. Verify:

```powershell
dotnet --version
# Should print 8.x.x
```

3. Optional for Docker demo: [Docker Desktop](https://www.docker.com/products/docker-desktop/)
4. Optional for BeamNG: install BeamNG.drive from Steam

### Linux / macOS

```bash
# Ubuntu/Debian example — install .NET 8 SDK per Microsoft docs for your distro
dotnet --version

# Optional Docker
docker --version
docker compose version
```

### Generate secrets (public demo / Docker)

```bash
openssl rand -hex 32   # use for NL_BUS_TOKEN
openssl rand -hex 32   # use for NL_OPERATOR_KEY
```

On Windows without OpenSSL, use Git Bash, WSL, or PowerShell:

```powershell
-join ((1..32) | ForEach-Object { '{0:x2}' -f (Get-Random -Max 256) })
```

---

## 6. Build and verify

From the repo root:

```bash
dotnet build src/NL.sln
dotnet test src/NL.sln
```

**Success:** build completes with no errors; tests pass (250+ tests).

If either fails, fix SDK version or network/proxy issues before continuing. See [§18 Troubleshooting](#18-troubleshooting).

---

## 7. Path A — Learn NL with no game (5 minutes)

Fastest way to understand Allow/Block/Warn without games, Docker, or Windows UI.

### Run the rule simulator

```bash
dotnet run --project src/NL.Simulator
dotnet run --project src/NL.Simulator -- samples/configs/full-session.nle
```

Example output:

```text
- PlayerA fires a weapon
    event:    shoot
    decision: Block
```

### Run the join-eligibility simulator

```bash
dotnet run --project src/NL.SpSimulator
```

Prints Allow / Deny / Hold for mock StreamPlayer profiles.

### Replay recorded sessions (CLI, still no live game)

```bash
# Minecraft sample log
dotnet run --project src/NL.Server -- --game minecraft \
  --config samples/configs/minecraft.nle \
  --source samples/logs/minecraft-sample.log \
  --replay

# Generic NDJSON
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/generic.nle \
  --source samples/events/generic-sample.ndjson \
  --replay

# Anti-cheat sample
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/anti-cheat.nle \
  --source samples/events/anti-cheat-sample.ndjson \
  --replay --anti-cheat
```

`--replay` means **no side effects** (no RCON kicks, no UDP actions). Safe for first runs.

### Edit a config and re-run

Open any file under `samples/configs/`, change a rule, save, re-run the simulator. Unknown events default to **Allow** — be explicit about blocks.

---

## 8. Path B — Windows streamer toolkit

For streamers who want real hotkeys, visual editing, and a one-click session shell.

### 8.1 Hotkey Daemon (background tray app)

**Requires:** Windows, .NET 8

```bash
dotnet run --project src/NL.HotkeyDaemon
```

- Tray icon appears (no main window). Right-click for Status, Reload, Open log, Exit.
- Default config: `%LOCALAPPDATA%\NL\hotkeys.nle` (auto-created from template if missing)
- Dev sample: `samples/configs/hotkeys.nle`
- Saves to the `.nle` file auto-reload within ~1 second

**Built-in hotkey actions:** mic mute, announce, master NL on/off, open log, OBS clip, focus OBS, mute desktop.

Details: [`docs/HOTKEY_DAEMON.md`](HOTKEY_DAEMON.md)

### 8.2 Config Editor (visual `.nle` authoring)

```bash
dotnet run --project src/NL.ConfigEditor
```

Build hotkey bindings and event rules visually; **Evaluate** runs the live rule engine on test events.

Opens `%LOCALAPPDATA%\NL\hotkeys.nle` by default (same file as the daemon).

### 8.3 Moderation Console

```bash
dotnet run --project src/NL.ModerationConsole
```

Review audit log; issue warning / ban / graylist / clear. Data under `%LOCALAPPDATA%\NL\`.

### 8.4 Session Host (recommended for live Windows sessions)

```bash
dotnet run --project src/NL.SessionHost
```

One **Start / Stop** UI for a full session:

1. Set game adapter (`minecraft` or `generic`)
2. Point **Config** at a `.nle` file
3. Point **Source** at log file or NDJSON path
4. Optional: RCON, join gate, anti-cheat, BeamNG UDP
5. **Start session**

**Tools menu** opens Moderation Console and Config Editor when published side-by-side (see §14).

For Minecraft and BeamNG step-by-step, see [§12](#12-path-f--live-minecraft-session) and [§13](#13-path-g--live-beamngdrive-session).

---

## 9. Path C — Web session server (local)

Cross-platform operator dashboard + session bus. No Docker required.

### Start the server

```bash
dotnet run --project src/NL.SessionHost.Web
```

### Open in your browser

| URL | Purpose |
|-----|---------|
| http://127.0.0.1:27020/ | Spectator landing (live feed when session running) |
| http://127.0.0.1:27020/operator.html | Operator console — start/stop session, manifest |
| http://127.0.0.1:27020/moderation.html | Moderation audit trail |
| http://127.0.0.1:27020/editor.html | Browser `.nle` editor |

### Optional: moderation web on separate port

```bash
dotnet run --project src/NL.Moderation.Web
# → http://127.0.0.1:27030
```

In Docker, moderation is merged into the session server on port 27020.

### Start a session from the UI

1. Open **operator.html**
2. Load or confirm session profile (`.nle` path, game adapter)
3. Click **Start session**
4. Connect a bridge (see below) or use spectator **try-a-rule** buttons

### Connect a game bridge (second terminal)

```bash
pip install websocket-client

python integrations/python/nl_bridge.py \
  --url "ws://127.0.0.1:27021/nl/v1?token=YOUR_TOKEN" \
  --admit-url "http://127.0.0.1:27020/api/v1/session/admit" \
  --sample
```

The bus token is printed at startup in the server console, or copy the **remote bridge manifest** from the operator dashboard.

Smoke test script:

```powershell
powershell -File scripts/nl-session-server-smoke.ps1
```

Details: [`docs/NL_SESSION_SERVER.md`](NL_SESSION_SERVER.md)

### Enable demo mode locally (auto-start + looping events)

Terminal 1:

```bash
export NL_DEMO_MODE=true
export NL_BUS_TOKEN=dev
export NL_OPERATOR_KEY=dev
dotnet run --project src/NL.SessionHost.Web
```

Terminal 2:

```bash
pip install websocket-client
python integrations/python/nl_bridge.py \
  --url "ws://127.0.0.1:27021/nl/v1?token=dev" \
  --loop --interval 8
```

Open http://127.0.0.1:27020/ — decisions should increment in the feed.

---

## 10. Path D — Docker demo loop (local)

Run the full demo stack (session server + auto demo loop + hardening) without Caddy/TLS. Good for CI-style verification or if you prefer containers over `dotnet run`.

### Prerequisites

- Docker + Compose v2
- Bash (Git Bash on Windows) for smoke scripts

### Start the stack

**Linux / macOS / Git Bash:**

```bash
export NL_BUS_TOKEN=$(openssl rand -hex 16)
export NL_OPERATOR_KEY=$(openssl rand -hex 16)
docker compose -f docker/docker-compose.demo-local.yml up -d --build --wait
```

**PowerShell:**

```powershell
$env:NL_BUS_TOKEN = -join ((1..16) | ForEach-Object { '{0:x2}' -f (Get-Random -Max 256) })
$env:NL_OPERATOR_KEY = -join ((1..16) | ForEach-Object { '{0:x2}' -f (Get-Random -Max 256) })
docker compose -f docker/docker-compose.demo-local.yml up -d --build --wait
```

### Open and verify

| URL | Expected |
|-----|----------|
| http://127.0.0.1:27020/ | Spectator page with live decisions |
| http://127.0.0.1:27020/health | `{"status":"ok",...}` |
| http://127.0.0.1:27020/api/v1/demo/status | `sessionRunning: true`, `decisions` > 0 after ~30s |

### Automated smoke test

```bash
bash scripts/nl-demo-smoke.sh
bash scripts/nl-hardening-smoke.sh
```

### Stop and reset

```bash
docker compose -f docker/docker-compose.demo-local.yml down
docker compose -f docker/docker-compose.demo-local.yml down -v   # also deletes data volume
```

---

## 11. Path E — Public internet demo (VPS)

Ship a **HTTPS demo** visitors can open in a browser: live decision feed, try-a-rule buttons, browser rule editor. Synthetic events from the demo bridge (not a live game stream).

### What you need

| Item | Details |
|------|---------|
| VPS | Ubuntu 22.04+ recommended |
| Domain | e.g. `demo.yourdomain.com` |
| DNS | A/AAAA record → VPS IP |
| Firewall | Ports **80** and **443** open |
| Docker + Compose v2 | On the VPS |

### Step 1 — Clone on the VPS

```bash
git clone https://github.com/Surrplexie/NexoraLive.git
cd NexoraLive
```

### Step 2 — Configure environment

```bash
cp docker/.env.demo.example docker/.env
```

Edit `docker/.env`:

```env
NL_DEMO_DOMAIN=demo.yourdomain.com
NL_BUS_TOKEN=<output of: openssl rand -hex 32>
NL_OPERATOR_KEY=<output of: openssl rand -hex 32>
CADDY_ACME_EMAIL=you@yourdomain.com
```

`deploy-demo.sh` can auto-fill `NL_PUBLIC_HTTP`, `NL_PUBLIC_WS`, etc. if you set `NL_DEMO_DOMAIN`.

### Step 3 — Deploy

```bash
bash scripts/deploy-demo.sh
```

**Windows (Docker Desktop + domain pointing at your machine):**

```powershell
Copy-Item docker\.env.demo.example docker\.env
# Edit docker\.env
powershell -File scripts\deploy-demo.ps1
```

### Step 4 — Verify

```bash
curl -fsS https://demo.yourdomain.com/health
curl -fsS https://demo.yourdomain.com/api/v1/demo/status
curl -fsS https://demo.yourdomain.com/api/v1/ops/status
```

### Step 5 — Open in browser

| URL | Audience |
|-----|----------|
| https://demo.yourdomain.com/ | Public — watch live rules |
| https://demo.yourdomain.com/editor.html | Public evaluate / operator save |
| https://demo.yourdomain.com/operator.html | Operators — enter `NL_OPERATOR_KEY` |
| https://demo.yourdomain.com/moderation.html | Audit trail |

**Store `NL_OPERATOR_KEY` in a password manager.** Do not commit secrets to git.

### Optional: pull pre-built image

In `docker/.env`:

```env
NL_SESSION_IMAGE=ghcr.io/surrplexie/nexoralive/session-server:latest
```

Then run `deploy-demo.sh` again.

### Architecture

```text
Internet → Caddy :443 (TLS)
              ├── /nl/v1*  → session-server:27021 (WebSocket bridge)
              └── /*       → session-server:27020 (HTTP + UI)
                    ↑
              demo-bridge (Python, loops sample events)
                    │
              volume nl-demo-data (/data)
```

More detail: [`docs/NL_DEPLOY.md`](NL_DEPLOY.md), [`docs/NL_DEMO.md`](NL_DEMO.md), [`docs/NL_DEMO_RUNBOOK.md`](NL_DEMO_RUNBOOK.md)

---

## 12. Path F — Live Minecraft session

**Requires:** Windows (Session Host) or any OS (CLI), Minecraft Java server with RCON, .NET 8

### Enable RCON

In `server.properties`:

```properties
enable-rcon=true
rcon.port=25575
rcon.password=your-secret
```

### Session Host (recommended)

```bash
dotnet run --project src/NL.SessionHost
```

1. Game = `minecraft`
2. Config = `samples/configs/minecraft.nle`
3. Source = path to your server `logs/latest.log`
4. RCON = `127.0.0.1:25575:your-secret` (leave empty for **dry-run** first)
5. Enable **Join gate** and **Anti-cheat** for full loop
6. **Start session**

### CLI equivalent

```bash
dotnet run --project src/NL.Server -- --game minecraft \
  --config samples/configs/minecraft.nle \
  --source "C:\path\to\logs\latest.log" \
  --rcon 127.0.0.1:25575:your-secret \
  --streamer default-streamer \
  --join-gate --anti-cheat
```

### Prove join gate (ban → kick)

1. Open Moderation Console; ban a test Minecraft username
2. Have that account join the server
3. NL should Block the join (RCON kick or dry-run log)

Seed a banned test profile without Minecraft:

```powershell
powershell -File scripts/seed-banned-eve.ps1
```

Full checklist: [`docs/MINECRAFT_LIVE.md`](MINECRAFT_LIVE.md)

---

## 13. Path G — Live BeamNG.drive session

**Requires:** Windows, BeamNG.drive (Steam), .NET 8, Session Host

### Install the Lua bridge mod

```powershell
powershell -File scripts/install-beamng-bridge.ps1
```

This packages `beamng-mod/NL_BeamNGBridge` into your BeamNG user folder:

- Typical path: `%LOCALAPPDATA%\BeamNG\BeamNG.drive\current\mods\`
- Events write to: `%LOCALAPPDATA%\NL\beamng-events.ndjson`

Override discovery: set `BEAMNG_USER_FOLDER` before running the install script.

Enable the mod in BeamNG, load a map.

### Session Host setup

```bash
dotnet run --project src/NL.SessionHost
```

1. **Tools → Load BeamNG freeroam defaults**
2. Confirm: game `generic`, config `beamng.nle`, source `beamng-events.ndjson`, UDP `127.0.0.1:27022`
3. Anti-cheat **on**, join gate **off** for solo freeroam
4. **Start session**, then drive in BeamNG

### Replay without the game

```bash
dotnet run --project src/NL.Server -- --game generic \
  --config samples/configs/beamng.nle \
  --source samples/events/beamng-sample.ndjson \
  --replay --anti-cheat
```

### BeamMP kicks (optional)

```powershell
powershell -File scripts/install-beammp-nl-kick.ps1
```

Full guide: [`docs/BEAMNG.md`](BEAMNG.md)

---

## 14. Portable publish (zip install)

Build self-contained folders you can zip and run without the full SDK on target machines (SDK still needed to **build**).

### Windows publish

```powershell
powershell -File scripts/publish.ps1
```

Output: `artifacts/publish/`

| Folder | Run |
|--------|-----|
| `SessionHost/` | `NL.SessionHost.exe` |
| `SessionHostWeb/` | `dotnet NL.SessionHost.Web.dll` |
| `ModerationWeb/` | `dotnet NL.Moderation.Web.dll` |
| `ModerationConsole/` | `NL.ModerationConsole.exe` |
| `ConfigEditor/` | `NL.ConfigEditor.exe` |
| `HotkeyDaemon/` | `NL.HotkeyDaemon.exe` |
| `Server/` | `NL.Server.exe` |

**Portable install steps:**

1. Run `publish.ps1` on a machine with .NET 8 SDK
2. Zip `artifacts/publish/`
3. Copy to target Windows PC
4. Install [.NET 8 **Runtime**](https://dotnet.microsoft.com/download/dotnet/8.0) (not SDK) on target if not self-contained
5. Run `SessionHost\NL.SessionHost.exe`

Session Host **Tools menu** finds `../ModerationConsole` and `../ConfigEditor` in this layout.

### Linux headless publish

```bash
bash scripts/publish-linux.sh
```

Output: `artifacts/publish-linux/` — run web DLLs with `dotnet` on Linux.

---

## 15. Ports, URLs, and what to open

| Port | Protocol | Service | When |
|------|----------|---------|------|
| **27020** | HTTP | Session server dashboard, REST API, spectator UI | Always (web stack) |
| **27021** | WebSocket | Session bus `/nl/v1?token=…` | Game bridges connect here |
| **27030** | HTTP | Standalone moderation web | When running `NL.Moderation.Web` separately |
| **27022** | UDP | BeamNG action sink | BeamNG live path only |
| **80 / 443** | HTTP/S | Caddy edge | Public demo (Path E) |
| **25575** | TCP | Minecraft RCON | Minecraft live kicks/tells |

### Health and status endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Liveness probe |
| `GET /api/v1/demo/status` | Demo loop running, decision count |
| `GET /api/v1/ops/status` | Rate limits, WebSocket metrics |
| `GET /api/v1/spectator/status` | Public spectator stats |
| `GET /api/v1/session/manifest` | Bridge URLs for integrators |

---

## 16. Data files and where NL stores state

### Windows default

All tools share **`%LOCALAPPDATA%\NL\`** unless overridden:

| File | Purpose |
|------|---------|
| `hotkeys.nle` | Hotkey daemon + editor default config |
| `hotkeys.log` | Hotkey action log |
| `session-profile.json` | Last Session Host profile |
| `sp-profiles.json` | StreamPlayer standing / offenses |
| `moderation.jsonl` | Decision + mod-action audit log |
| `join-requirements.json` | Join gate requirements |
| `beamng-events.ndjson` | BeamNG bridge event stream |
| `beamng-kicks.ndjson` | BeamMP kick queue |
| `web-editor-sandbox.nle` | Browser editor sandbox (web stack) |

Default streamer id: **`default-streamer`**

### Linux / Docker

Set **`NL_DATA_ROOT`**:

- Native Linux default: `~/.local/share/NL`
- Docker demo: `/data` (persistent volume `nl-demo-data`)

---

## 17. Environment variables cheat sheet

Copy templates from [`.env.example`](../.env.example) or [`docker/.env.demo.example`](../docker/.env.demo.example).

| Variable | Purpose |
|----------|---------|
| `NL_PUBLIC_MODE` | `true` = require fixed secrets before exposing to internet |
| `NL_BUS_TOKEN` | WebSocket bridge auth (`?token=` on `/nl/v1`) |
| `NL_OPERATOR_KEY` | Header `X-NL-Operator-Key` for writes |
| `NL_DEMO_MODE` | Auto-start session + periodic data reset |
| `NL_DEMO_DOMAIN` | Public hostname for Caddy |
| `NL_PUBLIC_HTTP` / `NL_PUBLIC_WS` | Manifest URLs behind TLS |
| `NL_DATA_ROOT` | Override all data file locations |
| `NL_BIND` | Listen address (`0.0.0.0` in Docker) |
| `NL_HTTP_PORT` / `NL_WS_PORT` | Web ports (default 27020 / 27021) |
| `NL_HARDENING` | Rate limits + WebSocket caps (on by default in public mode) |
| `CADDY_ACME_EMAIL` | Let's Encrypt contact for TLS |

Full security notes: [`docs/NL_DEMO_SECURITY.md`](NL_DEMO_SECURITY.md)

---

## 18. Troubleshooting

### Build / test failures

| Symptom | Fix |
|---------|-----|
| `dotnet: command not found` | Install .NET 8 SDK; restart terminal |
| Windows app won't run | Need Windows + `net8.0-windows` workload; use web stack on Linux |
| Test failures after local edits | `git status`; revert or fix broken changes |

### Web session server

| Symptom | Fix |
|---------|-----|
| Browser can't connect | Confirm server running; try http://127.0.0.1:27020/health |
| Bridge `401` / disconnect | Wrong `NL_BUS_TOKEN` in URL; copy from manifest |
| No decisions in feed | Session not started; connect bridge or enable `NL_DEMO_MODE` |
| `401` on start/stop | Set operator key in `/operator.html` or `X-NL-Operator-Key` header |

### Docker demo

| Symptom | Fix |
|---------|-----|
| Container unhealthy | `docker compose logs session-server` — often missing secrets when `NL_PUBLIC_MODE=true` |
| No decisions | Check `demo-bridge` logs; verify `NL_BUS_TOKEN` matches |
| Port already in use | Stop other NL instance or change host port mapping |

### Public demo (Caddy / TLS)

| Symptom | Fix |
|---------|-----|
| Certificate fails | DNS must point to VPS; ports 80/443 open; set `CADDY_ACME_EMAIL` |
| `429 Too Many Requests` | Expected under abuse; tune limits in `NL_*_RATE_PER_MIN` |

### Hotkey daemon

| Symptom | Fix |
|---------|-----|
| Second instance exits | Single-instance guard — only one daemon |
| Hotkey does nothing | Check `toggleNlEvents` master switch; read `%LOCALAPPDATA%\NL\hotkeys.log` |
| OBS clip fails | Enable OBS WebSocket server; configure `obs.json` next to `.nle` |

### Minecraft

| Symptom | Fix |
|---------|-----|
| No events | Wrong log path; server must be writing `latest.log` |
| No kicks | RCON empty (dry-run) or wrong host:port:password |
| Join gate ignored | Streamer id mismatch between Session Host and Moderation Console |

### BeamNG

| Symptom | Fix |
|---------|-----|
| Decisions stay 0 | Mod missing `scripts/modScript.lua`; re-run install script |
| No NDJSON file | Drive in-game after loading map with mod enabled |

---

## 19. What to read next

| Goal | Document |
|------|----------|
| Learn `.nle` language | [`NLE_GUIDE.md`](../NLE_GUIDE.md), [`docs/NLEVENT_LANGUAGE_SPEC_v0.1.md`](NLEVENT_LANGUAGE_SPEC_v0.1.md) |
| Architecture | [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) |
| Game integration contract | [`docs/NL_INTEGRATION_SPEC.md`](NL_INTEGRATION_SPEC.md) |
| Public demo security | [`docs/NL_DEMO_SECURITY.md`](NL_DEMO_SECURITY.md) |
| Spectator / editor UX | [`docs/NL_SPECTATOR.md`](NL_SPECTATOR.md), [`docs/NL_EDITOR.md`](NL_EDITOR.md) |
| Operator runbook | [`docs/NL_DEMO_RUNBOOK.md`](NL_DEMO_RUNBOOK.md) |
| Build status | [`ROADMAP.md`](../ROADMAP.md) |

---

## Quick reference — copy-paste startup commands

```bash
# Verify toolchain
dotnet build src/NL.sln && dotnet test src/NL.sln

# Fastest demo (no game)
dotnet run --project src/NL.Simulator

# Web dashboard (local)
dotnet run --project src/NL.SessionHost.Web
# → http://127.0.0.1:27020/

# Docker demo loop (local)
export NL_BUS_TOKEN=$(openssl rand -hex 16)
export NL_OPERATOR_KEY=$(openssl rand -hex 16)
docker compose -f docker/docker-compose.demo-local.yml up -d --build --wait
# → http://127.0.0.1:27020/

# Public demo (VPS)
cp docker/.env.demo.example docker/.env   # edit secrets + domain
bash scripts/deploy-demo.sh
# → https://YOUR_DOMAIN/
```

**Windows Session Host:** `dotnet run --project src/NL.SessionHost`  
**Windows publish:** `powershell -File scripts/publish.ps1`

---

*Last updated to match NexoraLive Phases A–K (public demo stack). If a command fails, check the linked doc for that component — behavior evolves with the prototype.*
