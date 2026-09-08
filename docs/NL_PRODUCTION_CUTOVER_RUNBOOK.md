# Production Cutover Runbook — Phase 10

Operator steps to move from local validation stacks to a public VPS.

## Pre-cutover

- [ ] Phase 9 launch ops validated locally
- [ ] Domain DNS pointed at VPS
- [ ] Steam Web API key issued
- [ ] Fork images built on VPS host
- [ ] Alert webhook URL ready

## Deploy

```bash
cp samples/fleet/production-cutover.env.example docker/production-cutover-fleet.env
# Edit: NL_OPERATOR_KEY, STEAM_WEB_API_KEY, NL_PUBLIC_BASE_URL, relay/TURN domains, webhook

docker compose -f docker/docker-compose.production-cutover.yml up -d --build
```

Ensure compose **overrides** or env file sets:

- `NL_PRODUCTION_CUTOVER_DEV=false`
- `NL_LIVE_PRODUCTION_DEV=false`
- `NL_LAUNCH_OPS_DEV=false`
- `NL_GA_ALLOW_MOCK_IDENTITY=false`

## Validate

```powershell
powershell -File scripts/nl-production-cutover-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -HttpsBaseUrl https://play.yourdomain.com `
  -OperatorKey "<NL_OPERATOR_KEY>"
```

Expected: **`PRODUCTION CUTOVER VALIDATION PASSED`**

## Post-cutover

1. Schedule daily `POST /api/v1/launch-ops/backup/run`
2. Confirm `/status.html` shows operational
3. Register streamer via `/ga.html` with live Steam link
4. Player join via `/nl-client.html`

## Rollback

```powershell
powershell -File scripts/nl-production-cutover-stack-down.ps1
# Re-enable launch-ops dev stack if needed:
powershell -File scripts/nl-launch-ops-stack-up.ps1
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `mock_identity_off` FAIL | Set `NL_GA_ALLOW_MOCK_IDENTITY=false` |
| `public_https_url` FAIL | Set real `NL_PUBLIC_BASE_URL`; set `NL_PRODUCTION_CUTOVER_DEV=false` only on VPS |
| `live_production_gate` FAIL | Run load test; ensure fleet SLOs met |
| `launch_ops_gate` FAIL | Configure webhook + run backup API |
| HTTPS probe fails | Fix Caddy TLS / DNS |
