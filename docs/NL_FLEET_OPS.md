# NL Fleet Operations & Scale (Phase S)

Production-scale controls for many concurrent ephemeral fork sessions: multi-region placement, relay/TURN masking, observability, autoscaling, incident runbooks, abuse gates, compliance, and staging SLOs.

## Quick start

```powershell
$env:NL_FLEET_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "mock"
# Local dev: bypass Twitch follower minimum (production default 50)
$env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
$env:NL_FLEET_DEV_TWITCH_FOLLOWERS = "100"
Copy-Item samples/fleet/streamer-requirements.json "$env:LOCALAPPDATA/NL/fleet/streamer-requirements.json"
dotnet run --project src/NL.SessionHost.Web
```

- **Operator dashboard:** `/fleet-ops.html`
- **Extended ops:** `GET /api/v1/ops/status` includes `fleet` block when enabled

## Multi-region placement

Streamers set `fleetPreferredRegion` on the session profile (`us-east`, `us-west`, `eu-west`) or pass `preferredRegion` on fork create.

`FleetRegionService.Place` picks nearest region using preferred region or geo hint (`eu`, `west`).

## Relay / TURN

When `NL_FLEET_ENABLED=true`, manifest `forkConnectEndpoint` is masked via relay template:

```
wss://relay-{region}.nl.example.com/fork/{session}
```

Manifest also exposes:

- `fleetRegionId` — assigned region
- `fleetTurnUri` — TURN server for NAT traversal

Raw host IPs are not exposed to players when masking is on (default).

## Observability

`JsonFleetMetricsStore` tracks:

- Total admits / denials
- Fork create rate (per minute)
- Per-session health samples
- Bus decision count (synced on admit)

Endpoints:

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/fleet/observability` | Metrics snapshot |
| GET | `/api/v1/fleet/slos` | Staging SLO evaluation |
| GET | `/api/v1/fleet/incidents` | Recent incidents |
| GET | `/api/v1/ops/status` | Phase K ops + fleet block |

## Autoscaling

`FleetAutoscaleService` evaluates warm pool vs scale-to-zero when no live streams and idle beyond `NL_FLEET` idle threshold (default 15 min). Integrated with `NlSocialHost` live monitor in `NlFleetLifecycleHostedService`.

## Incident runbook

Fork unhealthy (disconnected &gt;5 min idle) or crash:

1. Record incident with spectator message
2. Destroy session
3. Auto-recreate via `BusHostState.ProvisionForkSessionAsync` (abuse gate + placement)

## Abuse controls

| Control | Default | Env var |
|---------|---------|---------|
| Global fork creates / min | 30 | `NL_FLEET_FORK_CREATE_RATE_PER_MIN` |
| Min Twitch followers | 50 | `NL_FLEET_MIN_TWITCH_FOLLOWERS` |
| Per-streamer hourly quota | 6 | (policy) |
| Per-streamer requirements | JSON file | `NL_FLEET_ROOT/streamer-requirements.json` |

Fork create API accepts optional `twitchFollowers` for verification override.

## Backup & compliance

- **Moderation retention:** trims log lines older than `NL_FLEET_MOD_RETENTION_DAYS` (default 730)
- **GDPR export:** `POST /api/v1/fleet/compliance/export/{playerId}`
- **GDPR delete:** `DELETE /api/v1/fleet/compliance/sp/{playerId}`

## Staging SLOs

| SLO | Target |
|-----|--------|
| `concurrent_ephemeral_sessions` | ≥100 sessions |
| `admit_success_rate` | ≥99% |
| `fork_create_p99_ms` | ≤5000 ms (placeholder) |
| `incident_auto_restart_rate` | ≥95% |

Report load test results: `POST /api/v1/fleet/load-test/report`

Run smoke: `scripts/nl-fleet-load-test.ps1`

## Environment variables

See [`.env.example`](../.env.example) — `NL_FLEET_*` block.

## Exit criteria

100+ concurrent ephemeral sessions in staging with SLOs defined and observable via `/api/v1/fleet/slos` and `/fleet-ops.html`.
