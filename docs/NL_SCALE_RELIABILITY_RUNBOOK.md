# NL Scale & Reliability Runbook — Phase 12

Operator steps to validate GA-scale fleet capacity on production infrastructure.

## Prerequisites

- Phase 11 distribution stack validated
- Kubernetes or Docker orchestrator on VPS (mock acceptable for dev-only validation)
- TURN server reachable from players
- Per-region relay endpoints configured

## 1. Configure fleet env

Merge into `docker/scale-reliability-fleet.env`:

```env
NL_SCALE_RELIABILITY_ENABLED=true
NL_SCALE_RELIABILITY_DEV=false
NL_SCALE_RELIABILITY_MIN_CONCURRENT=128
NL_FLEET_FORK_CREATE_RATE_PER_MIN=200
NL_FLEET_MAX_FORK_CREATES_PER_HOUR=9999
NL_FLEET_RELAY_WS_TEMPLATE=wss://relay-{region}.yourdomain.com/fork/{session}
NL_FLEET_TURN_URI=turn:turn.yourdomain.com:3478
```

On VPS use real Docker/Kubernetes orchestrator (not mock).

## 2. Deploy stack

```powershell
powershell -File scripts/nl-scale-reliability-stack-up.ps1
```

## 3. Multi-region smoke

Create forks with `preferredRegion` set to each catalog region and confirm `regionId` in the response:

```powershell
# us-east, us-west, eu-west — see nl-scale-reliability-validate.ps1
```

## 4. Run GA load test

```powershell
powershell -File scripts/nl-scale-reliability-load-test.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -ConcurrentSessions 128
```

Review SLO output. On VPS, allow extra time for real container forks.

## 5. Run validation gate

```powershell
powershell -File scripts/nl-scale-reliability-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey "<key>"
```

With `NL_SCALE_RELIABILITY_DEV=false`, production SLOs and distribution gate must pass without dev shortcuts.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Load test creates fewer than 128 sessions | Raise `NL_FLEET_FORK_CREATE_RATE_PER_MIN` |
| `relay_region_template` fails | Add `{region}` to `NL_FLEET_RELAY_WS_TEMPLATE` |
| `distribution_gate` fails | Run distribution validation first |
| Admit burst failures | Check hardening limits and mock identity settings |

## Rollback

```powershell
powershell -File scripts/nl-scale-reliability-stack-down.ps1
docker compose -f docker/docker-compose.distribution.yml up -d
```
