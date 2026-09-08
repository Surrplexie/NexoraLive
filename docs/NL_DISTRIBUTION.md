# NL Player & Streamer Distribution — Phase 11

Ship installable **NL Client**, public onboarding pages, and auto-update manifest on top of the production cutover stack.

## Quick start (local distribution validation)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-distribution-stack-down.ps1
powershell -File scripts/nl-distribution-stack-up.ps1 -Validate
```

Expected: **`DISTRIBUTION VALIDATION PASSED`**

The stack-up script builds the Windows client zip into `wwwroot/downloads/` before `docker compose --build`, so the package is baked into the session-host image.

## What Phase 11 adds

| Feature | Description |
|---------|-------------|
| **Distribution program** | `NL_DISTRIBUTION_ENABLED` master switch |
| **Public landing** | `/play.html` — player entry point |
| **Client download** | `/download.html` + manifest API |
| **Auto-update manifest** | `GET /api/v1/distribution/client-manifest` |
| **Deep link scheme** | `nlclient://join?streamer=…&game=…&major=…` |
| **Streamer onboarding** | Open GA signup at `/ga.html` |
| **Cutover gate** | Distribution requires production cutover validation |
| **Validation API** | `GET/POST /api/v1/distribution/validation` |
| **Ops UI** | `/distribution-ops.html` |

Compose: [`docker/docker-compose.distribution.yml`](../docker/docker-compose.distribution.yml)

## Public pages

| URL | Purpose |
|-----|---------|
| `/play.html` | Public landing — download, join, streamer signup |
| `/download.html` | NL Client download + SHA256 + protocol registration |
| `/nl-client.html` | Web NL Client join UI |
| `/ga.html` | Streamer signup |
| `/identity-link.html` | Steam identity link |
| `/fork-catalog.html` | Browse supported games |
| `/distribution-ops.html` | Operator validation console |

## Build NL Client package

```powershell
powershell -File scripts/build-nl-client-package.ps1 -Version 1.0.0
```

Outputs:

- `src/NL.SessionHost.Web/wwwroot/downloads/nl-client-win-x64.zip`
- `src/NL.SessionHost.Web/wwwroot/downloads/nl-client-manifest.json`

## Register deep link (Windows)

```powershell
powershell -File scripts/register-nlclient-protocol.ps1
```

## Environment

```env
NL_DISTRIBUTION_ENABLED=true
NL_DISTRIBUTION_DEV=true              # local validation only
NL_DISTRIBUTION_CLIENT_VERSION=1.0.0
NL_DISTRIBUTION_WIN_PACKAGE=downloads/nl-client-win-x64.zip

NL_PRODUCTION_CUTOVER_ENABLED=true
NL_PRODUCTION_CUTOVER_DEV=false       # false on VPS
```

See [`samples/fleet/distribution.env.example`](../samples/fleet/distribution.env.example).

## Validation checks

- Distribution + production cutover programs enabled
- Production cutover gate passed (strict on VPS)
- Landing + download pages published
- Client manifest + Windows package on host
- `nlclient://` deep link documented
- Web client, streamer signup, identity link, catalog browser
- Streamer registration smoke + player join smoke

```powershell
powershell -File scripts/nl-distribution-validate.ps1 -OperatorKey "<from distribution-fleet.env>"
```

## Phase 11 exit criteria

- [x] Distribution compose stack (extends cutover)
- [x] NL Client package build script
- [x] Public landing + download pages
- [x] Client manifest API + deep link metadata
- [x] Distribution validation API + script
- [x] Ops UI + runbook
- [ ] CDN or signed release channel on VPS (operator deploy)

Next: **Phase 12** — scale & reliability ([NL_SCALE_RELIABILITY.md](NL_SCALE_RELIABILITY.md))

See also: [NL_DISTRIBUTION_RUNBOOK.md](NL_DISTRIBUTION_RUNBOOK.md) · [NL_PRODUCTION_CUTOVER.md](NL_PRODUCTION_CUTOVER.md) · [NL_SCALE_RELIABILITY.md](NL_SCALE_RELIABILITY.md)
