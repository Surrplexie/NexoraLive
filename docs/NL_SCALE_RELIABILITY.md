# NL Scale & Reliability — Phase 12

Multi-region fork placement and GA traffic load SLOs on top of the distribution stack.

## Quick start (local scale validation)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-scale-reliability-stack-down.ps1
powershell -File scripts/nl-scale-reliability-stack-up.ps1 -Validate
```

Expected: **`SCALE RELIABILITY VALIDATION PASSED`**

Local stack uses `NL_FORK_ORCHESTRATOR_MODE=mock` and elevated fork-create limits so 128 concurrent sessions validate in minutes.

## What Phase 12 adds

| Feature | Description |
|---------|-------------|
| **Scale program** | `NL_SCALE_RELIABILITY_ENABLED` master switch |
| **Multi-region catalog** | `GET /api/v1/scale-reliability/regions` (us-east, us-west, eu-west) |
| **Per-region relay** | Relay template must include `{region}` placeholder |
| **GA load test** | 128 concurrent fork sessions + admit burst |
| **Production SLOs** | 128 sessions, 99.5% admit, p99 fork create, 98% auto-restart |
| **Distribution gate** | Requires distribution validation upstream |
| **Validation API** | `GET/POST /api/v1/scale-reliability/validation` |
| **Ops UI** | `/scale-reliability-ops.html` |

Compose: [`docker/docker-compose.scale-reliability.yml`](../docker/docker-compose.scale-reliability.yml)

## Production SLO targets

| SLO | Target |
|-----|--------|
| `concurrent_ephemeral_sessions` | ≥128 |
| `admit_success_rate` | ≥99.5% |
| `fork_create_p99_ms` | ≤5000 ms |
| `incident_auto_restart_rate` | ≥98% |

## Environment

```env
NL_SCALE_RELIABILITY_ENABLED=true
NL_SCALE_RELIABILITY_DEV=true              # local validation only
NL_SCALE_RELIABILITY_MIN_CONCURRENT=128
NL_SCALE_RELIABILITY_MIN_REGIONS=3
NL_FLEET_FORK_CREATE_RATE_PER_MIN=200
NL_FLEET_RELAY_WS_TEMPLATE=wss://relay-{region}.yourdomain.com/fork/{session}
```

See [`samples/fleet/scale-reliability.env.example`](../samples/fleet/scale-reliability.env.example).

## Scripts

```powershell
# GA load test only (128 mock sessions)
powershell -File scripts/nl-scale-reliability-load-test.ps1

# Full validation gate
powershell -File scripts/nl-scale-reliability-validate.ps1 -OperatorKey "<key>"
```

## Phase 12 exit criteria

- [x] Scale reliability compose stack (extends distribution)
- [x] Multi-region placement smoke
- [x] GA load test script (128 sessions)
- [x] Production SLO validation API + script
- [x] Ops UI + runbook
- [ ] Multi-region VPS fleet (operator deploy)

Next: **Phase 13** — legal & compliance hardening for public GA.

See also: [NL_SCALE_RELIABILITY_RUNBOOK.md](NL_SCALE_RELIABILITY_RUNBOOK.md) · [NL_DISTRIBUTION.md](NL_DISTRIBUTION.md)
