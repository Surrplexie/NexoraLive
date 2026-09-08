# NL Distribution Runbook — Phase 11

Operator steps to publish NL Client and onboarding on a production VPS.

## Prerequisites

- Phase 10 production cutover stack validated on VPS
- Real HTTPS domain with Let's Encrypt (Caddy)
- Steam Web API key configured
- Fork images built on host (`hello-fork`, `minecraft`, `beamng`)

## 1. Build client release

On your build machine or CI:

```powershell
powershell -File scripts/build-nl-client-package.ps1 -Version 1.0.0
```

Verify SHA256 in `wwwroot/downloads/nl-client-manifest.json` matches the zip.

## 2. Configure fleet env

Copy examples and merge into `docker/distribution-fleet.env`:

```powershell
# From repo root on VPS
cp samples/fleet/distribution.env.example docker/distribution-fleet.env
# Also inherit production-cutover.env.example settings
```

Required on VPS:

```env
NL_DISTRIBUTION_ENABLED=true
NL_DISTRIBUTION_DEV=false
NL_DISTRIBUTION_CLIENT_VERSION=1.0.0
NL_PRODUCTION_CUTOVER_DEV=false
NL_PUBLIC_BASE_URL=https://play.yourdomain.com
STEAM_WEB_API_KEY=<real key>
NL_OPERATOR_KEY=<secure random>
```

## 3. Deploy stack

```powershell
powershell -File scripts/build-fork-images.ps1
docker compose -f docker/docker-compose.distribution.yml up -d --build
```

Ensure edge Caddyfiles point at your domain (not 127.0.0.1).

## 4. Smoke test public pages

| Check | URL |
|-------|-----|
| Landing | `https://play.yourdomain.com/play.html` |
| Download | `https://play.yourdomain.com/download.html` |
| Manifest | `GET /api/v1/distribution/client-manifest` |
| Streamer signup | `https://play.yourdomain.com/ga.html` |

## 5. Run validation

```powershell
powershell -File scripts/nl-distribution-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey "<key>"
```

On VPS with `NL_DISTRIBUTION_DEV=false`, all smokes and upstream cutover gate must pass.

## 6. Player install flow

1. Player visits `/download.html` and downloads `nl-client-win-x64.zip`
2. Player runs `scripts/register-nlclient-protocol.ps1` (or installer bundles it)
3. Streamer shares join link: `nlclient://join?streamer=<id>&game=hello-fork&major=1.0`
4. Or player uses `/nl-client.html` web join

## Rollback

```powershell
powershell -File scripts/nl-distribution-stack-down.ps1
# Revert to production-cutover compose only
docker compose -f docker/docker-compose.production-cutover.yml up -d
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `client_package` fails | Re-run `build-nl-client-package.ps1` before `docker compose --build` |
| `cutover_gate` fails | Run production cutover validation first; set real HTTPS URL |
| Dogfood join fails | Check fork images, `NL_FLEET_MIN_TWITCH_FOLLOWERS`, operator key |
| Manifest SHA mismatch | Rebuild zip + manifest together via build script |
