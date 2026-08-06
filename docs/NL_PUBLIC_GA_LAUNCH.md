# NL Public GA Launch — Phase 14

Final go-live checklist and operator runbook nesting the legal & compliance gate.

## Quick start (local validation)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-public-ga-launch-stack-down.ps1
powershell -File scripts/nl-public-ga-launch-stack-up.ps1 -Validate
```

Expected: **`PUBLIC GA LAUNCH VALIDATION PASSED`**

## What Phase 14 adds

| Feature | Description |
|---------|-------------|
| **Public GA launch program** | `NL_PUBLIC_GA_LAUNCH_ENABLED` master switch |
| **Launch checklist** | `/ga-launch-checklist.html` operator-facing checklist |
| **Operator signoff** | `POST /api/v1/public-ga-launch/signoff` records launch approval |
| **Legal compliance gate** | Requires legal validation upstream |
| **Backup + support checks** | Recent fleet backup and support contact verified |
| **Validation API** | `GET/POST /api/v1/public-ga-launch/validation` |
| **Ops UI** | `/public-ga-launch-ops.html` |

Compose: [`docker/docker-compose.public-ga-launch.yml`](../docker/docker-compose.public-ga-launch.yml)

## Public pages

| URL | Purpose |
|-----|---------|
| `/ga-launch-checklist.html` | GA launch checklist |
| `/play.html` | Public landing |
| `/download.html` | NL Client download |
| `/status.html` | Public status page |
| `/public-ga-launch-ops.html` | Operator launch console |

## Environment

```env
NL_PUBLIC_GA_LAUNCH_ENABLED=true
NL_PUBLIC_GA_LAUNCH_DEV=true              # local validation only
NL_PUBLIC_GA_LAUNCH_VERSION=2026-08-01
NL_PUBLIC_GA_SUPPORT_CONTACT=support@yourdomain.com
NL_LEGAL_COMPLIANCE_ENABLED=true
```

See [`samples/fleet/public-ga-launch.env.example`](../samples/fleet/public-ga-launch.env.example).

## Validation checks

- Public GA launch program enabled
- Legal & compliance validation gate passed
- All upstream release programs enabled (GA, distribution, scale, legal, launch ops, cutover)
- GA open signup enabled
- Public landing, download, status, and checklist pages published
- Support contact configured
- Recent fleet backup verified
- Operator launch signoff recorded
- Launch version configured

```powershell
powershell -File scripts/nl-public-ga-launch-validate.ps1 -OperatorKey "<key>"
```

## Phase 14 exit criteria

- [x] Public GA launch compose stack (extends legal compliance)
- [x] Launch checklist + ops UI
- [x] Operator signoff store + validation API
- [x] Validation script + unit tests
- [x] Runbook + deploy README
- [x] Production dogfood local gate — [`docs/NL_PRODUCTION_DOGFOOD.md`](NL_PRODUCTION_DOGFOOD.md)
- [ ] Production VPS signoff with `NL_PUBLIC_GA_LAUNCH_DEV=false` (operator deploy)

See also: [`docs/NL_PUBLIC_GA_LAUNCH_RUNBOOK.md`](NL_PUBLIC_GA_LAUNCH_RUNBOOK.md)
