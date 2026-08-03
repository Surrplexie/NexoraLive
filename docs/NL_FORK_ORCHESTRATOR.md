# NL Fork Orchestrator (Phase O)

Spin up and tear down **one ephemeral forked game server per stream session**. The Session Host
control plane provisions a fork instance (mock, local process, or Docker), injects streamer
`.nle` + moderation pointers, wires the session bus, and destroys world state on teardown.

## Quick start

```powershell
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "mock"
$env:NL_FORK_CATALOG_ENABLED = "true"
dotnet run --project src/NL.SessionHost.Web
```

Open **Fork orchestrator** UI: `http://127.0.0.1:27020/fork-orchestrator.html`

Enable orchestrator on the session profile (`forkOrchestratorEnabled: true`) or select a catalog
game with **Enable orchestrator** — then **Start session** from Operator console.

## Provisioners

| Mode | Env `NL_FORK_ORCHESTRATOR_MODE` | Behavior |
|------|----------------------------------|----------|
| Mock | `mock` | Writes `fork-status.json` in workspace (no external process) |
| Process | `process` | Runs `NL.Fork.Runtime` via `dotnet` |
| Docker | `docker` | `docker run nl-fork-hello:latest` with workspace volume |
| Kubernetes | `kubernetes` / `k8s` | Job + ConfigMap per session via `kubectl` |
| Auto | `auto` (default) | Process if runtime DLL built, else Mock |

Docker is optional; mock + process cover local dev without Docker installed.

Docker containers reach the session host via `host.docker.internal` (override with `NL_FORK_DOCKER_HOST`).
The provisioner rewrites `127.0.0.1` / `localhost` in bridge and admit URLs automatically.

## Lifecycle

1. **Create** — on session start when `forkOrchestratorEnabled` is true
2. **Grace destroy** — on session stop (`NL_FORK_DESTROY_GRACE_SEC`, default 30s)
3. **Max duration** — `NL_FORK_SESSION_MAX_HOURS` (default 12h)
4. **Stream end** — when `requireLiveStream` and social gate detect offline
5. **Idle** — placeholder hook when fork status reports disconnected (`NL_FORK_IDLE_MINUTES`)

World data under `fork-orchestrator/sessions/{id}/world/` is deleted on destroy. Shared
moderation JSONL, SP profiles, and `.nle` remain under `NL_DATA_ROOT`.

## Manifest fields

`GET /api/v1/session/manifest` includes:

| Field | Description |
|-------|-------------|
| `forkOrchestratorEnabled` | Profile flag |
| `forkSessionId` | Ephemeral fork session id |
| `forkConnectEndpoint` | `mock://`, `process://`, or `docker://` URI |
| `forkProvisioner` | Mock / Process / Docker |
| `reservedPrivilegedSlots` | nl.txt privileged slot reservation |

## REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/fork/orchestrator/settings` | Public orchestrator config |
| GET | `/api/v1/fork/orchestrator/sessions` | Active fork sessions |
| GET | `/api/v1/fork/orchestrator/sessions/{id}` | Single session |
| POST | `/api/v1/fork/orchestrator/create` | Manual create (operator) |
| POST | `/api/v1/fork/orchestrator/destroy/{id}` | Force destroy |

## Environment

```bash
NL_FORK_ORCHESTRATOR_ENABLED=true
NL_FORK_ORCHESTRATOR_MODE=mock
NL_FORK_DESTROY_GRACE_SEC=30
NL_FORK_SESSION_MAX_HOURS=12
NL_FORK_RESERVED_PRIVILEGED_SLOTS=2
NL_FORK_DOCKER_IMAGE=nl-fork-hello:latest
NL_FORK_K8S_NAMESPACE=nl-fork
NL_FORK_K8S_KUBECONFIG=
NL_FORK_ORCHESTRATOR_ROOT=/data/fork-orchestrator
```

## Tests

```powershell
dotnet test tests/NL.Fork.Orchestrator.Tests -c Release
./scripts/nl-fork-orchestrator-smoke.ps1
```

## Exit criteria

Streamer starts session → orchestrator runs a fork instance → bridge connects → stream ends →
instance gone; config files remain in `NL_DATA_ROOT`.
