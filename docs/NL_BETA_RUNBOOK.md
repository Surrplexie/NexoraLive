# NL Public Beta — Operator Runbook (Phase 5)

Day-to-day operations for the hosted public beta.

## Daily checks

```bash
curl -fsS https://beta.yourdomain.com/health
curl -fsS https://beta.yourdomain.com/api/v1/beta/status
curl -fsS https://beta.yourdomain.com/api/v1/fleet/observability
```

Watch `/fleet-ops.html` for fork health, admit denials, and SLOs.

## Approve a streamer

1. Open `https://beta.yourdomain.com/beta-ops.html`
2. Enter `NL_OPERATOR_KEY` (browser session storage)
3. Review waitlist table
4. Click **Approve** — creates streamer ID `beta-{entryId}` unless you override via API

API:

```bash
curl -X POST "https://beta.yourdomain.com/api/v1/beta/waitlist/{entryId}/approve" \
  -H "X-NL-Operator-Key: $NL_OPERATOR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"streamerId":"their-twitch-name"}'
```

Pre-approved operator streamers (no waitlist): set `NL_BETA_OPERATOR_STREAMERS` env.

## Streamer go-live checklist

1. Streamer approved on waitlist
2. Streamer links Steam at `/identity-link.html`
3. Operator loads dogfood profile or custom NLE on `/operator.html`
4. Enable **Fork orchestrator**
5. **Start session** — verify fork on `/fork-orchestrator.html`
6. Share `/nl-client.html` with viewers

## Player join troubleshooting

| Symptom | Fix |
|---------|-----|
| `SessionOffline` | Operator must start session first |
| Ownership denied | Link Steam; verify game App ID in profile |
| Fork create denied (beta) | Approve streamer on waitlist |
| 401 on start/stop | Operator key missing in UI |

## Secret rotation

1. Generate new `NL_OPERATOR_KEY` and `NL_BUS_TOKEN`
2. Update `docker/beta-fleet.env`
3. `docker compose -f docker/docker-compose.beta-fleet.yml up -d`
4. Share new operator key with staff only

## Validation before invite wave

```powershell
powershell -File scripts/nl-beta-validate.ps1 -OperatorKey "..."
powershell -File scripts/nl-production-validate.ps1
```

Both should pass before opening waitlist publicly.

## Teardown / reset beta data

```powershell
powershell -File scripts/nl-beta-stack-down.ps1 -RemoveVolumes
```

Clears `/data` including waitlist JSON at `fleet/beta-waitlist.json`.

See also: [NL_PUBLIC_BETA.md](NL_PUBLIC_BETA.md)
