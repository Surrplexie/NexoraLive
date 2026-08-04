# NL Production Fleet — Phase 4

Run **real container forks** (not mock) behind the production readiness gate: `NL_FLEET_PRODUCTION_READY=true`.

## Quick start (local Docker Desktop)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

# Stop staging stack if running (same ports)
powershell -File scripts/nl-staging-stack-down.ps1

powershell -File scripts/nl-production-stack-up.ps1 -Validate
```

Expected: **`PRODUCTION VALIDATION PASSED`** — 100 concurrent **Docker** fork containers, all SLOs green, production orchestrator check passed.

### Stack only

```powershell
powershell -File scripts/nl-production-stack-up.ps1
powershell -File scripts/nl-production-validate.ps1
```

### Teardown

```powershell
powershell -File scripts/nl-production-stack-down.ps1
# optional: -RemoveVolumes
```

## What differs from Phase 3 (staging)

| Setting | Staging (Phase 3) | Production (Phase 4) |
|---------|-------------------|----------------------|
| `NL_FORK_ORCHESTRATOR_MODE` | `mock` | **`docker`** or **`kubernetes`** |
| `NL_FLEET_PRODUCTION_READY` | `false` | **`true`** |
| `NL_FLEET_STAGING_DEV` | `true` | **`false`** |
| Fork containers | In-process mock | **Real `nl-fork-hello` images** |
| Production gate | Staging SLOs only | + Docker/K8s orchestrator + non-placeholder relay |

Compose file: [`docker/docker-compose.production-fleet.yml`](../docker/docker-compose.production-fleet.yml)

## Architecture

```
                    ┌─────────────┐
  HTTPS :443 ──────►│ edge Caddy  │──► session-host :27020
                    └─────────────┘
  WSS :8443 ───────►│ relay stub  │──► session-host :27021
                    └─────────────┘
  TURN :3478 ──────►│ coturn      │
                    └─────────────┘

  session-host (docker.sock) ──► nl-fork-{session} containers (hello-fork runtime)
```

Session host mounts the host Docker socket and bind-mounts `./docker/production-data` so fork workspace volumes resolve correctly (`NL_FORK_DOCKER_WORKSPACE_HOST_ROOT`).

## Alternative: host session host + edge compose

If you prefer the session host on the host OS (same as dogfood):

```powershell
# Terminal 1 — build fork image once
docker build -f docker/fork-hello/Dockerfile -t nl-fork-hello:latest .

# Terminal 2 — edge/relay/coturn only (edit compose to omit session-host) OR use production stack
powershell -File scripts/nl-production-host-local.ps1

# Terminal 3
powershell -File scripts/nl-production-validate.ps1 -NlePath "samples\configs\fork-hello.nle"
```

## Kubernetes production cluster

For multi-node fleets, use the Kubernetes provisioner:

```bash
kubectl apply -f deploy/k8s/production/
```

Manifests set `NL_FORK_ORCHESTRATOR_MODE=kubernetes` and `NL_FLEET_PRODUCTION_READY=true`.

See [`deploy/production/README.md`](../deploy/production/README.md).

## Production validation gate

Same SLOs as staging, plus:

| Check | Requirement |
|-------|-------------|
| Orchestrator | **Docker** or **Kubernetes** (not Mock) |
| Relay | Non-`example.com` template |
| TURN | Configured |
| `productionReady` | All checks pass |

API: `GET /api/v1/fleet/validation` · UI: `/fleet-ops.html`

Script: [`scripts/nl-production-validate.ps1`](../scripts/nl-production-validate.ps1)

## VPS deploy

1. Copy [`samples/fleet/production.env.example`](../samples/fleet/production.env.example) → `docker/production-fleet.env`
2. Set `NL_PUBLIC_BASE_URL`, relay/TURN domains, `NL_OPERATOR_KEY`, `STEAM_WEB_API_KEY`, `NL_OWNERSHIP_MODE=live`
3. Point DNS at VPS; use Caddy + Let's Encrypt ([`deploy/staging/README.md`](../deploy/staging/README.md) templates)
4. `docker compose -f docker/docker-compose.production-fleet.yml up -d --build`
5. `powershell -File scripts/nl-production-validate.ps1` from an operator machine

## Phase 4 exit criteria

- [x] Production compose stack with Docker fork provisioner
- [x] Host workspace path mapping for docker.sock volume mounts
- [x] `NL_FLEET_PRODUCTION_READY=true` validation gate
- [x] K8s production manifests
- [x] 100-session production validation script
- [ ] VPS with real domain (operator deploy step)

Next: **Phase 6** — general availability (multi-game catalog, SLA, compliance).

See also: [NL_PUBLIC_BETA.md](NL_PUBLIC_BETA.md) · [NL_STAGING_HOSTED.md](NL_STAGING_HOSTED.md) · [NL_FLEET_OPS.md](NL_FLEET_OPS.md)
