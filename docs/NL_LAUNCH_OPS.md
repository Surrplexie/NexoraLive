# NL Launch Ops & Trust — Phase 9

Alerting, public status page, legal pages, abuse hardening, and fleet backups on top of the multi-game production stack.

## Quick start (local dev validation)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-launch-ops-stack-down.ps1
powershell -File scripts/nl-launch-ops-stack-up.ps1 -Validate
```

Expected: **`LAUNCH OPS VALIDATION PASSED`**

## What Phase 9 adds

| Feature | Description |
|---------|-------------|
| **Launch ops program** | `NL_LAUNCH_OPS_ENABLED` master switch |
| **Public status page** | `/status.html` + `GET /api/v1/launch-ops/status` |
| **Legal pages** | `/terms.html`, `/privacy.html` with version from `NL_LAUNCH_LEGAL_VERSION` |
| **Abuse hardening** | Requires `NL_HARDENING=true` + fleet fork-create limits |
| **Backups** | `POST /api/v1/launch-ops/backup/run` + manifest verification |
| **Alerting** | Optional `NL_LAUNCH_ALERT_WEBHOOK_URL` + test endpoint |
| **Validation API** | `GET/POST /api/v1/launch-ops/validation` |
| **Ops UI** | `/launch-ops.html` |

Compose: [`docker/docker-compose.launch-ops.yml`](../docker/docker-compose.launch-ops.yml)

## Public pages

| URL | Purpose |
|-----|---------|
| `/status.html` | Component health (API, identity, orchestrator, hardening) |
| `/terms.html` | Terms of Service |
| `/privacy.html` | Privacy Policy + GDPR endpoints |
| `/launch-ops.html` | Operator validation + backup + alert test |

## Environment

```env
NL_LAUNCH_OPS_ENABLED=true
NL_LAUNCH_OPS_DEV=true          # local validation only
NL_LAUNCH_STATUS_PAGE_ENABLED=true
NL_LAUNCH_LEGAL_VERSION=2026-08-01
NL_LAUNCH_BACKUP_ROOT=/data/backups
NL_LAUNCH_ALERT_WEBHOOK_URL=    # Slack/Discord webhook (production)
NL_HARDENING=true
```

See [`samples/fleet/launch-ops.env.example`](../samples/fleet/launch-ops.env.example).

## Validation checks

- Launch ops + multi-game programs enabled
- Multi-game validation gate passed
- Hardening enabled (`NL_HARDENING`)
- Fleet abuse limits configured
- Status page + legal pages reachable
- Recent fleet backup manifest
- GDPR export/delete + 730-day retention
- Alerting configured (production) or dev mode

```powershell
powershell -File scripts/nl-launch-ops-validate.ps1 -OperatorKey "<from launch-ops-fleet.env>"
```

## Phase 9 exit criteria

- [x] Launch ops compose stack
- [x] Status page + legal pages
- [x] Backup API + validation gate
- [x] Alert webhook test endpoint
- [x] Ops UI + runbook
- [ ] Production webhook + scheduled backups on VPS

See also: [NL_LAUNCH_OPS_RUNBOOK.md](NL_LAUNCH_OPS_RUNBOOK.md) · [NL_MULTI_GAME_PRODUCTION.md](NL_MULTI_GAME_PRODUCTION.md) · [NL_HARDENING.md](NL_HARDENING.md)
