# NL Public Demo — Operator Runbook (Phase K)

Quick reference for running and maintaining the NexoraLive public demo stack (Phases F–K).

## Deploy

```bash
cp docker/.env.demo.example docker/.env
# Set NL_DEMO_DOMAIN, NL_BUS_TOKEN, NL_OPERATOR_KEY, CADDY_ACME_EMAIL
bash scripts/deploy-demo.sh
```

Verify:

```bash
curl -fsS https://YOUR_DOMAIN/health
curl -fsS https://YOUR_DOMAIN/api/v1/ops/status
curl -fsS https://YOUR_DOMAIN/api/v1/demo/status
```

Visitors open `/` (spectator). Operators use `/operator.html` with `NL_OPERATOR_KEY`.

## Secret rotation

1. Generate new secrets: `openssl rand -hex 32`
2. Update `docker/.env` (`NL_BUS_TOKEN`, `NL_OPERATOR_KEY`)
3. Redeploy: `bash scripts/deploy-demo.sh`
4. Restart demo-bridge sidecar (picks up new bus token from compose)
5. Share new operator key with trusted operators only

**Note:** Changing `NL_BUS_TOKEN` requires restarting both `session-server` and `demo-bridge`.

## Reset demo data manually

Clears moderation audit + SP profiles (same as periodic Phase G reset):

```bash
docker compose -f docker/docker-compose.demo.yml exec session-server \
  sh -c 'echo -n "" > /data/moderation.jsonl && echo "[]" > /data/sp-profiles.json"'
docker compose -f docker/docker-compose.demo.yml restart session-server demo-bridge
```

Or destroy the volume (full reset):

```bash
docker compose -f docker/docker-compose.demo.yml down -v
bash scripts/deploy-demo.sh
```

## Connect your own game bridge

1. Operator-authenticate and copy bridge URL from `/operator.html` manifest (includes `?token=`).
2. Point your bridge at `wss://YOUR_DOMAIN/nl/v1?token=NL_BUS_TOKEN`.
3. Optional: call `POST /api/v1/session/admit` before `playerJoin` when join gate is on.

See [NL Integration Spec v1](NL_INTEGRATION_SPEC.md) and [NL Session Server](NL_SESSION_SERVER.md).

## Monitoring

| Endpoint | Use |
|----------|-----|
| `GET /health` | Uptime probe (load balancers, Docker healthcheck) |
| `GET /api/v1/ops/status` | Hardening metrics, WS connections, demo state |
| `GET /api/v1/demo/status` | Demo loop running + decision count |

Watch for:

- `sessionRunning: false` for extended periods → check `session-server` logs
- Rising `rateLimits.admitRejected` → possible abuse or misconfigured bridge
- `webSocket.activeConnections` at max → extra bridges blocked until disconnect

## Hardening knobs (Phase K)

| Variable | Default (public) | Purpose |
|----------|------------------|---------|
| `NL_HARDENING` | on when `NL_PUBLIC_MODE` | Master switch |
| `NL_ADMIT_RATE_PER_MIN` | 120 | `POST /api/v1/session/admit` |
| `NL_PUBLIC_READ_RATE_PER_MIN` | 300 | Spectator + moderation reads |
| `NL_WS_MAX_CONNECTIONS` | 16 | Total bridge WebSockets |
| `NL_WS_MAX_CONNECTIONS_PER_IP` | 4 | Per-IP bridge cap |
| `NL_WS_CONNECT_RATE_PER_MIN` | 30 | WS connect attempts per IP |
| `NL_DEMO_SESSION_MAX_HOURS` | 12 | Auto-restart long sessions (0=off) |

See [NL Demo Hardening](NL_HARDENING.md) for details.

## Troubleshooting

| Symptom | Action |
|---------|--------|
| 429 on admit/trigger | Expected under abuse; raise limits or wait 60s |
| Demo feed empty | Check `/api/v1/demo/status` → `sessionRunning`; restart `demo-bridge` |
| Caddy TLS fails | DNS, ports 80/443, `CADDY_ACME_EMAIL` |
| Operator 401 | Set key in dashboard or `X-NL-Operator-Key` header |

## CI smoke scripts

```bash
bash scripts/nl-demo-smoke.sh        # Phase G loop
bash scripts/nl-hardening-smoke.sh   # Phase K rate limits
```

## Related docs

- [NL Deploy (Phase F)](NL_DEPLOY.md)
- [NL Demo Loop (Phase G)](NL_DEMO.md)
- [NL Spectator UX (Phase H)](NL_SPECTATOR.md)
- [NL Demo Security (Phase E)](NL_DEMO_SECURITY.md)
- [NL Hardening (Phase K)](NL_HARDENING.md)
