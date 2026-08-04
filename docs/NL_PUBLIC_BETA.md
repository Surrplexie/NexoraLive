# NL Public Beta — Phase 5

Open signup waitlist, live Steam identity, and operator runbooks on top of the production fleet stack.

## Quick start (local)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

# Stop other stacks using port 27020
powershell -File scripts/nl-beta-stack-down.ps1

powershell -File scripts/nl-beta-stack-up.ps1 -Validate
```

Expected: **`BETA VALIDATION PASSED`**

## What Phase 5 adds

| Feature | Description |
|---------|-------------|
| **Waitlist** | Streamers sign up at `/beta.html` |
| **Allowlist** | Only approved streamers can create fork sessions |
| **Live identity** | `NL_OWNERSHIP_MODE=live` + Steam Web API on VPS |
| **Public mode** | Operator key required for session control |
| **Beta ops UI** | `/beta-ops.html` — approve waitlist, run validation |
| **Validation gate** | `GET/POST /api/v1/beta/validation` |

Compose file: [`docker/docker-compose.beta-fleet.yml`](../docker/docker-compose.beta-fleet.yml)

## Operator pages

| URL | Purpose |
|-----|---------|
| `/beta.html` | Public waitlist signup |
| `/beta-ops.html` | Approve streamers, validation (needs `NL_OPERATOR_KEY`) |
| `/identity-link.html` | Link Steam account |
| `/nl-client.html` | Player join flow |
| `/operator.html` | Start/stop sessions |

## VPS deploy

1. Copy [`samples/fleet/beta.env.example`](../samples/fleet/beta.env.example) into `docker/beta-fleet.env`
2. Set secrets: `NL_OPERATOR_KEY`, `NL_BUS_TOKEN`, `STEAM_WEB_API_KEY`
3. Set `NL_BETA_ALLOW_MOCK_IDENTITY=false` and `NL_BETA_REQUIRE_PRODUCTION_READY=true`
4. Point DNS + Caddy (see [`deploy/beta/README.md`](../deploy/beta/README.md))
5. `docker compose -f docker/docker-compose.beta-fleet.yml up -d --build`
6. Approve first streamers at `/beta-ops.html`

## Validation

```powershell
powershell -File scripts/nl-beta-validate.ps1 -OperatorKey "<from beta-fleet.env>"
```

Checks:

- Beta program enabled + waitlist open
- Operator key + public mode
- Live Steam identity (or mock allowed locally)
- Waitlist signup + approve flow
- Unapproved streamer blocked from fork create
- Approved streamer dogfood join smoke
- Beta validation gate API

## Phase 5 exit criteria

- [x] Beta compose stack on production fleet foundation
- [x] Waitlist API + public signup page
- [x] Streamer allowlist on fork create
- [x] Beta validation script + API gate
- [x] Operator runbook
- [ ] VPS with real domain + live Steam (operator deploy)

Next: **Phase 6** — general availability (multi-game catalog, SLA, compliance).

See also: [NL_BETA_RUNBOOK.md](NL_BETA_RUNBOOK.md) · [NL_PRODUCTION_FLEET.md](NL_PRODUCTION_FLEET.md)
