# NL General Availability — Operator Runbook (Phase 6)

Day-to-day operations for the hosted GA fleet.

## Daily checks

```bash
curl -fsS https://play.yourdomain.com/health
curl -fsS https://play.yourdomain.com/api/v1/ga/status
curl -fsS https://play.yourdomain.com/api/v1/ga/sla
curl -fsS https://play.yourdomain.com/api/v1/fleet/observability
```

Watch `/fleet-ops.html` and `/ga-ops.html` for fork health, SLA status, and admit denials.

## Streamer onboarding

1. Streamer registers at `https://play.yourdomain.com/ga.html`
2. Note their **Streamer ID** from the confirmation
3. Streamer links Steam at `/identity-link.html`
4. Operator or streamer loads NLE profile on `/operator.html`
5. Enable **Fork orchestrator** and select catalog game
6. **Start session** — verify fork on `/fork-orchestrator.html`
7. Share `/nl-client.html` with viewers

## Multi-game catalog

Supported GA titles (default):

| Game ID | Display name | Docker image |
|---------|--------------|--------------|
| `hello-fork` | Hello Fork Runtime | `nl-fork-hello:latest` |
| `minecraft` | Minecraft Java | `nl-fork-minecraft:latest` |
| `beamng` | BeamNG.drive | `nl-fork-beamng:latest` |

Catalog manifest: `NL_FORK_CATALOG_MANIFEST` (default `/app/samples/fork/catalog.json` in container).

Browse: `/fork-catalog.html` or `GET /api/v1/ga/catalog`.

## Production SLA

Monitor at `/ga-ops.html` or `GET /api/v1/ga/sla`.

If SLOs breach:

1. Check `/api/v1/fleet/incidents` for fork crashes
2. Review admit denial rate in observability
3. Scale warm pool or increase `NL_FLEET_MAX_CONCURRENT`
4. Verify relay/TURN endpoints are reachable

## Compliance

- **GDPR export:** `POST /api/v1/fleet/compliance/export/{playerId}`
- **GDPR delete:** `DELETE /api/v1/fleet/compliance/sp/{playerId}`
- **Retention:** `NL_FLEET_MOD_RETENTION_DAYS` (default 730)

Exports stored under `{NL_FLEET_ROOT}/compliance-exports/`.

## Player join troubleshooting

| Symptom | Fix |
|---------|-----|
| `SessionOffline` | Operator must start session first |
| Ownership denied | Link Steam; verify game App ID in profile |
| Catalog version mismatch | Update client major or select stable catalog entry |
| 401 on start/stop | Operator key missing in UI |

## Secret rotation

1. Generate new `NL_OPERATOR_KEY` and `NL_BUS_TOKEN`
2. Update `docker/ga-fleet.env`
3. `docker compose -f docker/docker-compose.ga-fleet.yml up -d`
4. Share new operator key with staff only

## Validation before launch

```powershell
powershell -File scripts/nl-ga-validate.ps1 -OperatorKey "..."
powershell -File scripts/nl-production-validate.ps1
```

Both should pass before opening GA publicly.

## Teardown / reset GA data

```powershell
powershell -File scripts/nl-ga-stack-down.ps1 -RemoveVolumes
```

Clears `/data` including streamer registry at `fleet/ga-streamers.json`.

See also: [NL_GENERAL_AVAILABILITY.md](NL_GENERAL_AVAILABILITY.md)
