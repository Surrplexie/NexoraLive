# NL Fleet Staging → Production Validation (Phase S)

Validate **100+ concurrent ephemeral fork sessions** with defined SLOs before promoting fleet ops to production.

## Quick validation (local)

```powershell
# Terminal 1 — session host with fleet + mock orchestrator
$env:NL_FLEET_ENABLED = "true"
$env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
$env:NL_FLEET_FORK_CREATE_RATE_PER_MIN = "120"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "mock"
dotnet run --project src/NL.SessionHost.Web

# Terminal 2 — load test + validation gate
powershell -File scripts/nl-fleet-staging-validation.ps1 -ConcurrentSessions 100
```

Or use the **staging docker stack** (session host + HTTPS edge + relay stub + coturn):

```powershell
powershell -File scripts/nl-staging-stack-up.ps1 -Validate
# or manually:
docker compose -f docker/docker-compose.staging-fleet.yml up --build -d
powershell -File scripts/nl-staging-validate.ps1
```

See [NL_STAGING_HOSTED.md](NL_STAGING_HOSTED.md) for Phase 3 hosted staging (VPS, HTTPS, secrets).

## Staging SLOs

| SLO | Target | Source |
|-----|--------|--------|
| `concurrent_ephemeral_sessions` | ≥100 | Active fork sessions during load test |
| `admit_success_rate` | ≥99% | Admit burst success ratio |
| `fork_create_p99_ms` | ≤5000 ms | Fork create latency samples |
| `incident_auto_restart_rate` | ≥95% | Incident store auto-restart flag |

View live: `/fleet-ops.html` or `GET /api/v1/fleet/slos`

## Validation API

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/fleet/validation` | Run readiness checklist (no persist) |
| POST | `/api/v1/fleet/validation/run` | Evaluate + save to `fleet/validation-last.json` |
| POST | `/api/v1/fleet/load-test/report` | Submit load test results + SLO evaluation |

### Staging vs production gates

| Check | Staging | Production (`NL_FLEET_PRODUCTION_READY=true`) |
|-------|---------|-----------------------------------------------|
| 100+ concurrent sessions | Required | Required |
| Relay not `example.com` | Optional with `NL_FLEET_STAGING_DEV=true` | Required |
| Orchestrator | Mock OK if load test proves scale | **Docker** or **Kubernetes** required |
| TURN configured | Required | Required |

## Kubernetes staging cluster

Manifests under [`deploy/k8s/staging/`](../deploy/k8s/staging/):

```bash
kubectl apply -f deploy/k8s/staging/
# Build/push nl-session-host:latest and nl-fork-hello:latest to your registry first
kubectl -n nl-fleet-staging set env deployment/nl-session-host NL_FORK_DOCKER_IMAGE=your-registry/nl-fork-hello:latest
```

The session host ServiceAccount can create Jobs + ConfigMaps in namespace `nl-fork` via the Kubernetes fork provisioner (`NL_FORK_ORCHESTRATOR_MODE=kubernetes`).

## Load test script

[`scripts/nl-fleet-staging-validation.ps1`](../scripts/nl-fleet-staging-validation.ps1):

1. Creates N fork sessions (unique streamer IDs, multi-region placement)
2. Runs admit burst in parallel
3. Posts results to `/api/v1/fleet/load-test/report`
4. Prints SLO + validation checklist
5. Cleans up fork sessions (unless `-SkipCleanup`)

Parameters:

| Param | Default | Description |
|-------|---------|-------------|
| `-BaseUrl` | `http://127.0.0.1:27020` | Session host URL |
| `-ConcurrentSessions` | `100` | Fork sessions to create |
| `-AdmitBurst` | `50` | Parallel admit requests |
| `-SkipCleanup` | off | Leave fork sessions running |

Unit tests + optional live run:

```powershell
powershell -File scripts/nl-fleet-load-test.ps1
powershell -File scripts/nl-fleet-load-test.ps1 -Live
```

## Environment variables

See [`samples/fleet/staging.env.example`](../samples/fleet/staging.env.example) and [`.env.example`](../.env.example) `NL_FLEET_*` / `NL_FORK_K8S_*` blocks.

## Exit criteria

Phase S exit criteria met when:

- Load test creates **100+ concurrent fork sessions**
- All **staging SLOs** pass (observable via `/api/v1/fleet/slos`)
- **`stagingPassed: true`** on validation report
- Production promotion additionally requires non-placeholder relay/TURN and Docker/Kubernetes orchestrator — see [NL_PRODUCTION_FLEET.md](NL_PRODUCTION_FLEET.md)

See also: [NL_FLEET_OPS.md](NL_FLEET_OPS.md)
