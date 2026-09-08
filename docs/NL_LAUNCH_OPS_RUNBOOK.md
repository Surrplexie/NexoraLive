# Launch Ops Runbook — Phase 9

Operator checklist for public launch readiness.

## Local validation

```powershell
powershell -File scripts/nl-launch-ops-stack-down.ps1
powershell -File scripts/nl-launch-ops-stack-up.ps1 -Validate
```

## VPS deploy

1. Copy `samples/fleet/launch-ops.env.example` → `docker/launch-ops-fleet.env`
2. Set operator key, Steam key, public URL, alert webhook
3. Set `NL_LAUNCH_OPS_DEV=false`, `NL_GA_ALLOW_MOCK_IDENTITY=false`
4. Configure cron for backups:

```bash
# Example: daily backup via API (use operator key)
0 3 * * * curl -fsS -X POST -H "X-NL-Operator-Key: $NL_OPERATOR_KEY" \
  https://play.yourdomain.com/api/v1/launch-ops/backup/run
```

5. Start stack:

```bash
docker compose -f docker/docker-compose.launch-ops.yml up -d --build
```

6. Validate:

```powershell
powershell -File scripts/nl-launch-ops-validate.ps1 -BaseUrl https://play.yourdomain.com -OperatorKey "<key>"
```

## Alerting

Set `NL_LAUNCH_ALERT_WEBHOOK_URL` to a Slack/Discord incoming webhook. Test from ops UI or:

```powershell
Invoke-RestMethod -Method POST -Uri "http://127.0.0.1:27020/api/v1/launch-ops/alert/test" `
  -Headers @{ "X-NL-Operator-Key" = "<key>" }
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `hardening` FAIL | Set `NL_HARDENING=true` |
| `backup` FAIL | Run `POST /api/v1/launch-ops/backup/run` or check manifest age |
| `multigame_gate` FAIL | Fix Phase 8 first |
| `alerting` FAIL (prod) | Set webhook URL or run test alert |
| `legal_pages` FAIL | Ensure terms/privacy HTML deployed |

## Rollback

```powershell
powershell -File scripts/nl-launch-ops-stack-down.ps1
powershell -File scripts/nl-multi-game-stack-up.ps1
```
