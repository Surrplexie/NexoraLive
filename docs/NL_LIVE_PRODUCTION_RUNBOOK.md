# NL Live Production — Operator Runbook (Phase 7)

Operations for the internet-facing GA fleet with live Steam identity.

## Pre-launch validation

```bash
curl -fsS https://play.yourdomain.com/health
curl -fsS https://play.yourdomain.com/api/v1/live-production/status
curl -fsS https://play.yourdomain.com/api/v1/identity/settings
```

Identity settings should report `"mode": "Live"` and `"steamConfigured": true`.

PowerShell gate:

```powershell
powershell -File scripts/nl-live-production-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -HttpsBaseUrl https://play.yourdomain.com `
  -OperatorKey "..."
```

## Steam OpenID

`NL_PUBLIC_BASE_URL` must match the URL users visit. Steam OpenID callback:

`https://play.yourdomain.com/api/v1/identity/oauth/steam/callback`

Test: open `/identity-link.html`, complete Steam login, verify link succeeds.

## DNS records

| Host | Purpose |
|------|---------|
| `play.yourdomain.com` | Session host API + UI (HTTPS :443) |
| `relay-us-east.yourdomain.com` | Fork WebSocket relay |
| `relay-us-west.yourdomain.com` | West region relay |
| `relay-eu-west.yourdomain.com` | EU relay |
| `turn.yourdomain.com` | coturn (UDP/TCP 3478) |

## Environment (production)

```bash
NL_LIVE_PRODUCTION_ENABLED=true
NL_LIVE_PRODUCTION_DEV=false
NL_GA_ALLOW_MOCK_IDENTITY=false
NL_GA_REQUIRE_PRODUCTION_READY=true
NL_OWNERSHIP_MODE=live
STEAM_WEB_API_KEY=<real-key>
NL_PUBLIC_BASE_URL=https://play.yourdomain.com
```

## Secret rotation

1. Generate new `NL_OPERATOR_KEY`, `NL_BUS_TOKEN`
2. Update `docker/live-production-fleet.env` or K8s secret
3. `docker compose -f docker/docker-compose.live-production.yml up -d`
4. Re-run live production validation

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Identity mode Mock | Set `STEAM_WEB_API_KEY`; restart session-host |
| Steam OpenID fails | `NL_PUBLIC_BASE_URL` must match browser URL |
| HTTPS cert error | Caddy needs valid DNS; use Let's Encrypt on VPS |
| Relay connect fails | Verify `relay-*` DNS and `NL_FLEET_RELAY_WS_TEMPLATE` |
| Validation fails on localhost check | Set `NL_LIVE_PRODUCTION_DEV=false` only on VPS |

## Teardown

```powershell
powershell -File scripts/nl-live-production-stack-down.ps1 -RemoveVolumes
```

See also: [NL_LIVE_PRODUCTION.md](NL_LIVE_PRODUCTION.md)
