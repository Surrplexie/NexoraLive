# NL Live Production — Phase 7

Deploy GA on the **public internet** with live Steam identity, HTTPS edge, relay/TURN, and no mock shortcuts.

## Quick start (local dev validation)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-live-production-stack-down.ps1
powershell -File scripts/nl-live-production-stack-up.ps1 -Validate
```

Expected: **`LIVE PRODUCTION VALIDATION PASSED`**

Local stack uses `NL_LIVE_PRODUCTION_DEV=true` (127.0.0.1 URLs, mock allowed for join smoke). VPS uses `NL_LIVE_PRODUCTION_DEV=false`.

## What Phase 7 adds

| Feature | Description |
|---------|-------------|
| **Live Steam** | `STEAM_WEB_API_KEY` + `NL_OWNERSHIP_MODE=live` (no mock fallback without key) |
| **HTTPS edge** | Caddy TLS for API + static UI |
| **Production relay/TURN** | Real `wss://relay-{region}.yourdomain.com` templates |
| **Live production gate** | `GET/POST /api/v1/live-production/validation` |
| **VPS / K8s templates** | `deploy/live-production/`, `deploy/k8s/live-production/` |
| **Ops UI** | `/live-production-ops.html` |

Compose: [`docker/docker-compose.live-production.yml`](../docker/docker-compose.live-production.yml)

## Operator pages

| URL | Purpose |
|-----|---------|
| `https://play.yourdomain.com/ga.html` | Streamer registration |
| `https://play.yourdomain.com/identity-link.html` | Link Steam (OpenID callback on public URL) |
| `https://play.yourdomain.com/nl-client.html` | Player join |
| `https://play.yourdomain.com/live-production-ops.html` | Validation + status |

## VPS checklist

1. Copy [`samples/fleet/live-production.env.example`](../samples/fleet/live-production.env.example) → `docker/live-production-fleet.env`
2. Set `NL_PUBLIC_BASE_URL=https://play.yourdomain.com`
3. Set `STEAM_WEB_API_KEY` from [Steam Web API](https://steamcommunity.com/dev/apikey)
4. Set `NL_LIVE_PRODUCTION_DEV=false`, `NL_GA_ALLOW_MOCK_IDENTITY=false`, `NL_GA_REQUIRE_PRODUCTION_READY=true`
5. Replace Caddyfiles in `docker/edge-production/` and `docker/relay-production/` with real domains
6. Point DNS: `play`, `relay-us-east`, `relay-us-west`, `relay-eu-west`, `turn`
7. `docker compose -f docker/docker-compose.live-production.yml up -d --build`
8. Run validation against public URL

## Validation checks

- Live production + GA enabled, beta disabled
- Steam Web API key configured, identity mode Live
- Mock identity disabled (production)
- HTTPS public base URL (not localhost)
- Relay template uses production host (not example.com)
- TURN URI configured
- Multi-game catalog + compliance
- Dogfood smoke (local dev only with `-Validate`)

```powershell
powershell -File scripts/nl-live-production-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -HttpsBaseUrl https://play.yourdomain.com `
  -OperatorKey "<from live-production-fleet.env>"
```

## Phase 7 exit criteria

- [x] Live production compose stack (GA + live Steam config)
- [x] HTTPS edge + relay production Caddy templates
- [x] Live production validation API + script
- [x] VPS + K8s deploy templates
- [x] Operator runbook
- [ ] VPS with real domain + live Steam (operator deploy)

Next: **Phase 8** — multi-game production fork images + player UX on public URL.

See also: [NL_LIVE_PRODUCTION_RUNBOOK.md](NL_LIVE_PRODUCTION_RUNBOOK.md) · [NL_GENERAL_AVAILABILITY.md](NL_GENERAL_AVAILABILITY.md)
