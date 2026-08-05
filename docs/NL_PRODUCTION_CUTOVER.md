# NL Production Cutover — Phase 10

Flip from **local dev validation** to **production VPS config**: all dev shortcuts off, real HTTPS domain, live Steam only.

## Quick start (local cutover validation)

Uses production flag values with `NL_PRODUCTION_CUTOVER_DEV=true` so 127.0.0.1 URLs still validate locally.

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-production-cutover-stack-down.ps1
powershell -File scripts/nl-production-cutover-stack-up.ps1 -Validate
```

Expected: **`PRODUCTION CUTOVER VALIDATION PASSED`**

## What Phase 10 adds

| Feature | Description |
|---------|-------------|
| **Cutover program** | `NL_PRODUCTION_CUTOVER_ENABLED` |
| **Dev flags off** | Verifies `NL_LIVE_PRODUCTION_DEV`, `NL_LAUNCH_OPS_DEV`, `NL_GA_ALLOW_MOCK_IDENTITY` are false |
| **Production GA** | `NL_GA_REQUIRE_PRODUCTION_READY=true`, `NL_GA_REQUIRE_LIVE_IDENTITY=true` |
| **HTTPS probe** | Operator script hits edge `/health` over TLS |
| **Upstream gates** | Live production, multi-game, launch ops (strict on VPS) |
| **Validation API** | `GET/POST /api/v1/production-cutover/validation` |
| **Ops UI** | `/production-cutover-ops.html` |

Compose: [`docker/docker-compose.production-cutover.yml`](../docker/docker-compose.production-cutover.yml)

## Production flags (must all be set on VPS)

```env
NL_PRODUCTION_CUTOVER_DEV=false
NL_LIVE_PRODUCTION_DEV=false
NL_LAUNCH_OPS_DEV=false
NL_GA_ALLOW_MOCK_IDENTITY=false
NL_GA_REQUIRE_PRODUCTION_READY=true
NL_GA_REQUIRE_LIVE_IDENTITY=true
NL_PUBLIC_BASE_URL=https://play.yourdomain.com
STEAM_WEB_API_KEY=<real key>
NL_LAUNCH_ALERT_WEBHOOK_URL=<slack or discord webhook>
```

See [`samples/fleet/production-cutover.env.example`](../samples/fleet/production-cutover.env.example).

## VPS cutover checklist

1. Copy env example → `docker/production-cutover-fleet.env`
2. Set real domain, Steam key, operator key, alert webhook
3. Replace Caddyfiles with Let's Encrypt domains
4. Build/pull fork images on host
5. `docker compose -f docker/docker-compose.production-cutover.yml up -d --build`
6. Run validation against public URL:

```powershell
powershell -File scripts/nl-production-cutover-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -HttpsBaseUrl https://play.yourdomain.com `
  -OperatorKey "<key>"
```

On VPS set `NL_PRODUCTION_CUTOVER_DEV=false` — validation **requires** real HTTPS hostname and all upstream gates.

## Phase 10 exit criteria

- [x] Production cutover compose (dev shortcuts off in container env)
- [x] Cutover validation API + script
- [x] HTTPS edge probe
- [x] Ops UI + runbook
- [ ] Real VPS with domain + Let's Encrypt (operator deploy)

Next: **Phase 11** — player & streamer distribution (installable NL Client, onboarding).

See also: [NL_PRODUCTION_CUTOVER_RUNBOOK.md](NL_PRODUCTION_CUTOVER_RUNBOOK.md) · [NL_LAUNCH_OPS.md](NL_LAUNCH_OPS.md)
