# NL General Availability — Phase 6

Open streamer signup, multi-game catalog, production SLA, and compliance on top of the production fleet stack.

## Quick start (local)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

# Stop other stacks using port 27020
powershell -File scripts/nl-ga-stack-down.ps1

powershell -File scripts/nl-ga-stack-up.ps1 -Validate
```

Expected: **`GA VALIDATION PASSED`**

## What Phase 6 adds

| Feature | Description |
|---------|-------------|
| **Open signup** | Streamers register at `/ga.html` — no waitlist |
| **Multi-game catalog** | hello-fork, Minecraft, BeamNG via fork catalog |
| **Production SLA** | Stricter SLO targets at `/api/v1/ga/sla` |
| **Compliance** | GDPR export/delete + 730-day moderation retention |
| **Beta disabled** | `NL_BETA_ENABLED=false` when GA is active |
| **GA ops UI** | `/ga-ops.html` — validation, SLA, streamers |
| **Validation gate** | `GET/POST /api/v1/ga/validation` |

Compose file: [`docker/docker-compose.ga-fleet.yml`](../docker/docker-compose.ga-fleet.yml)

## Operator pages

| URL | Purpose |
|-----|---------|
| `/ga.html` | Public streamer registration |
| `/ga-ops.html` | SLA, validation, registered streamers |
| `/fork-catalog.html` | Browse supported games |
| `/identity-link.html` | Link Steam account |
| `/nl-client.html` | Player join flow |
| `/operator.html` | Start/stop sessions |
| `/fleet-ops.html` | Fleet observability |

## Production SLA targets

| SLO | Target |
|-----|--------|
| `concurrent_ephemeral_sessions` | ≥128 sessions |
| `admit_success_rate` | ≥99.5% |
| `fork_create_p99_ms` | ≤15000 ms (Docker production) |
| `incident_auto_restart_rate` | ≥98% |

## VPS deploy

1. Copy [`samples/fleet/ga.env.example`](../samples/fleet/ga.env.example) into `docker/ga-fleet.env`
2. Set secrets: `NL_OPERATOR_KEY`, `NL_BUS_TOKEN`, `STEAM_WEB_API_KEY`
3. Set `NL_GA_ALLOW_MOCK_IDENTITY=false` and `NL_GA_REQUIRE_PRODUCTION_READY=true`
4. Ensure `NL_BETA_ENABLED=false`
5. Point DNS + Caddy (see [`deploy/ga/README.md`](../deploy/ga/README.md))
6. `docker compose -f docker/docker-compose.ga-fleet.yml up -d --build`
7. Run validation before launch: `powershell -File scripts/nl-ga-validate.ps1`

## Validation

```powershell
powershell -File scripts/nl-ga-validate.ps1 -OperatorKey "<from ga-fleet.env>"
```

Checks:

- GA enabled, beta disabled
- Multi-game catalog (hello-fork, minecraft, beamng)
- Open streamer registration + fork create (no allowlist)
- Compliance export endpoint
- Dogfood join smoke
- GA validation gate API

## Phase 6 exit criteria

- [x] GA compose stack on production fleet foundation
- [x] Open streamer signup API + public page
- [x] Multi-game catalog enabled with required titles
- [x] Production SLA tier + API
- [x] Compliance policy (GDPR + retention)
- [x] GA validation script + API gate
- [x] Operator runbook
- [ ] VPS with real domain + live Steam (operator deploy) — see [NL_LIVE_PRODUCTION.md](NL_LIVE_PRODUCTION.md)

Next: **Phase 7** — live production deploy ([NL_LIVE_PRODUCTION.md](NL_LIVE_PRODUCTION.md)).
