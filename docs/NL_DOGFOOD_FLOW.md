# NL Dogfood Flow — Operator → Client Join → Teardown

One full end-to-end pass through the NL fork platform: streamer starts session with ephemeral fork, SP joins via NL Client, stream ends and fork is destroyed.

## Prerequisites

- .NET 8 SDK
- Session Host built at least once: `dotnet build src/NL.SessionHost.Web`

## One-command automated dogfood

**Terminal 1** — start session host (one command):

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive
powershell -File scripts/nl-session-host-docker-dogfood.ps1 -NoBuild
```

Or inline (same env vars):

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive; $env:NL_FLEET_ENABLED="true"; $env:NL_FLEET_MIN_TWITCH_FOLLOWERS="0"; $env:NL_FLEET_MAX_FORK_CREATES_PER_HOUR="999"; $env:NL_FORK_ORCHESTRATOR_ENABLED="true"; $env:NL_FORK_ORCHESTRATOR_MODE="docker"; $env:NL_IDENTITY_ENABLED="true"; $env:NL_OWNERSHIP_MODE="mock"; $env:NL_PUBLIC_BASE_URL="http://127.0.0.1:27020"; dotnet run --project src/NL.SessionHost.Web -c Release --no-build
```

Wait for `Now listening on: http://127.0.0.1:27020`.

**Terminal 2** — run the dogfood script:

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive
powershell -File scripts/nl-dogfood-flow.ps1
```

Expected output: `DOGFOOD FLOW PASSED` with setup → start → join → teardown steps green.

### Process-mode dogfood (real fork runtime)

**Terminal 1** — process orchestrator (spawns `NL.Fork.Runtime`):

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

$env:NL_FLEET_ENABLED = "true"
$env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "process"
$env:NL_IDENTITY_ENABLED = "true"
$env:NL_OWNERSHIP_MODE = "mock"

dotnet build src/NL.Fork.Runtime -c Release
dotnet run --project src/NL.SessionHost.Web
```

**Terminal 2**:

```powershell
powershell -File scripts/nl-dogfood-flow-process.ps1
# or: powershell -File scripts/nl-dogfood-flow.ps1 -ExpectProvisioner process
```

Expect `provisioner=Process` and `forkConnect=process://localhost/{pid}` in the start step.

### Docker-mode dogfood (hello-fork, Phase 1.1)

Requires **Docker Desktop** running.

**Terminal 1** — docker orchestrator:

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

$env:NL_FLEET_ENABLED = "true"
$env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
$env:NL_FLEET_MAX_FORK_CREATES_PER_HOUR = "999"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "docker"
$env:NL_IDENTITY_ENABLED = "true"
$env:NL_OWNERSHIP_MODE = "mock"

dotnet run --project src/NL.SessionHost.Web
```

**Terminal 2** — builds `nl-fork-hello:latest` (unless `-SkipImageBuild`) and runs the flow:

```powershell
powershell -File scripts/nl-dogfood-flow-docker.ps1
# or: powershell -File scripts/nl-dogfood-flow.ps1 -ExpectProvisioner docker
```

Expect `provisioner=Docker` and `forkConnect=docker://nl-fork-{sessionId}`.

### Docker-mode dogfood (minecraft sidecar, Phase 1.2)

Same Terminal 1 env as hello-fork docker (`NL_FORK_ORCHESTRATOR_MODE=docker`).

**Terminal 2**:

```powershell
powershell -File scripts/nl-dogfood-flow-docker-minecraft.ps1
```

Builds `nl-fork-minecraft:latest`, loads `minecraft.nle`, and verifies at least one rule decision from the fork demo loop (`playerJoin` warn, caps chat block, etc.).

Expect `forkConnect=minecraft://127.0.0.1:25565`.

---

## Operator checklist (manual browser flow)

Use this when driving the flow from **operator.html** instead of the automated script.

### Before start

1. Session host env: `NL_FORK_ORCHESTRATOR_ENABLED=true` and mode set (`mock` / `process` / `docker`).
2. Open **http://127.0.0.1:27020/operator.html**
3. Click **Load dogfood profile** — confirm:
   - Streamer: `dogfood-streamer`
   - Config: path to `fork-hello.nle` (or `minecraft.nle` for minecraft dogfood)
   - **Fork orchestrator** checkbox **checked** (required — without it no fork is provisioned)
   - Game id: `hello-fork` (or `minecraft` for Phase 1.2)
4. Click **Load bus defaults** if source path is empty.

### Start and verify

5. Click **Start session**
6. Confirm manifest shows:
   - `sessionRunning: yes`
   - `forkOrchestratorEnabled: true`
   - `forkSessionId` set
   - `forkConnectEndpoint` set (`mock://…`, `process://…`, `docker://…`, or `minecraft://…`)
7. Optional: **http://127.0.0.1:27020/fork-orchestrator.html** — 1 active fork session.

### Common mistake

| Symptom | Cause | Fix |
|---------|--------|-----|
| Session runs but no fork | Fork orchestrator unchecked on profile | **Load dogfood profile** again (sets checkbox) or check **Fork orchestrator** manually before Start |

---

## Manual browser flow (same path)

### Step 0 — Environment

Use the same env vars as Terminal 1 above. Mock ownership matrix is copied automatically on setup (`steam:76561198000000001` owns app `440`).

### Step 1 — Operator: load dogfood profile

Open **http://127.0.0.1:27020/operator.html**

1. Click **Load dogfood profile** (or call `POST /api/v1/dogfood/setup`)
2. Confirm fields:
   - Streamer: `dogfood-streamer`
   - Config: path to `fork-hello.nle`
   - **Fork orchestrator** checked
   - Game id: `hello-fork`

### Step 2 — Operator: start session

1. Click **Load bus defaults** (if source path empty)
2. Click **Start session**
3. Confirm manifest shows:
   - `sessionRunning: yes`
   - `forkOrchestratorEnabled: true`
   - `forkSessionId` set
   - `forkConnectEndpoint` (mock://…)

Optional: **http://127.0.0.1:27020/fork-orchestrator.html** — 1 active fork session.

### Step 3 — Identity (optional but realistic)

Open **http://127.0.0.1:27020/identity-link.html**

1. Create account → copy `accountId`
2. Or skip and use mock Steam64 directly: `76561198000000001`

### Step 4 — NL Client: join

Open **http://127.0.0.1:27020/nl-client.html**

| Field | Value |
|-------|--------|
| Streamer | `dogfood-streamer` (click LIVE in list after start) |
| Player id | `sp-dogfood-1` |
| Platform user (Steam64) | `76561198000000001` |
| NL account id | from identity linker (optional) |

Click **Run join flow**.

Success shows `step: "Completed"` with `launch` containing `forkConnectEndpoint` and `bridgeConnectUrl`.

CLI equivalent:

```powershell
dotnet run --project src/NL.Client -- join `
  --player sp-dogfood-1 `
  --streamer dogfood-streamer `
  --platform-user 76561198000000001 `
  --game hello-fork `
  --major 1.0
```

### Step 5 — Verify in-session

- **Overlay:** refresh on NL Client page
- **Moderation:** http://127.0.0.1:27020/moderation.html
- **Fleet metrics:** http://127.0.0.1:27020/fleet-ops.html (admit recorded)

### Step 6 — Teardown (stream ends)

**Operator console** → click **Stop session**.

What happens:

1. NLS session bus stops
2. Fork orchestrator schedules grace destroy (default 30s)
3. Mock fork workspace wiped
4. `forkSessionId` cleared from profile

Verify:

```powershell
Invoke-RestMethod http://127.0.0.1:27020/api/v1/fork/orchestrator/sessions
# → empty array after grace period

Invoke-RestMethod http://127.0.0.1:27020/api/v1/dogfood/status
# → sessionRunning: false, activeForkSessions: 0
```

---

## API reference (dogfood)

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/dogfood/setup` | Load dogfood profile + mock ownership |
| GET | `/api/v1/dogfood/status` | Checklist snapshot |
| POST | `/api/v1/session/start` | Start NLS + provision fork |
| POST | `/api/v1/client/join-flow` | Full SP join pipeline |
| POST | `/api/v1/session/stop` | Stop session + destroy fork |

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `/health` returns 500 | Restart session host (identity DELETE route fix) |
| Join denied — ownership | Use Steam64 `76561198000000001`; run dogfood setup for mock matrix |
| Join denied — at-own-risk | Dogfood profile disables partnership gate; check **At-own-risk** if using catalog tier |
| Session won't start — config missing | Run **Load dogfood profile** or `POST /api/v1/dogfood/setup` |
| Fork not created | `NL_FORK_ORCHESTRATOR_ENABLED=true` and profile `forkOrchestratorEnabled: true` |
| Docker provision fails | Docker Desktop running; image built (`nl-fork-hello:latest` or `nl-fork-minecraft:latest`) |
| Fork container exits immediately | URLs must reach host from container — provisioner rewrites to `host.docker.internal` automatically |
| Minecraft port conflict | Stop other containers using host port 25565 |
| Stale fork blocks start | Script auto-resets; or `POST /api/v1/session/stop` then destroy via fork orchestrator UI |
| Fork create hourly quota exceeded | Dogfood default is 6/hour — set `NL_FLEET_MAX_FORK_CREATES_PER_HOUR=999` on session host, restart, or delete `%LOCALAPPDATA%\NL\fleet\metrics.json` before restart |
| MSB3027 build / file locked | Stop running `NL.SessionHost.Web` first (Task Manager or close Terminal 1), then rebuild |
| Streamer offline in client | Start session first — `isLive` follows running session |

## Next after dogfood

- Switch `NL_FORK_ORCHESTRATOR_MODE=process` or `docker` for real fork runtime
- Docker hello-fork: `scripts/nl-dogfood-flow-docker.ps1`
- Docker minecraft sidecar: `scripts/nl-dogfood-flow-docker-minecraft.ps1`
- Enable `requireGameOwnership` + Steam OpenID via `/identity-link.html`
- Run staging validation: `scripts/nl-fleet-staging-validation.ps1`
