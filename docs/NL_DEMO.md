# NL Hosted Demo Loop (Phase G)

Phase G turns the Phase F deploy stack into a **turnkey public demo**: a live session starts automatically, a sidecar bridge emits sample game events in a loop, and moderation data resets on a schedule so the instance stays fresh.

## What visitors see

1. Open the demo URL (HTTPS via Caddy, or `http://localhost` for local compose).
2. The dashboard shows a **Live public demo** banner.
3. **Decisions** increment as Allow / Block / Warn events appear in the session log.
4. **Moderation → recent actions** fills with automatic audit entries (read-only for visitors).
5. Operators with `NL_OPERATOR_KEY` can still start/stop or issue mod actions.

No manual bridge setup, no operator key required to **watch** the demo work.

## Architecture

```text
docker-compose.demo.yml
├── session-server (NL_DEMO_MODE=true)
│     ├── NlDemoHostedService → reset data, apply demo.nle, auto-start session
│     └── WebSocket bus :27021 ← game events
├── demo-bridge (Python nl_bridge.py --loop)
│     └── connects internal ws://session-server:27021/nl/v1?token=…
└── caddy → TLS + reverse proxy to dashboard + /nl/v1
```

## Environment variables

| Variable | Default (demo compose) | Purpose |
|----------|------------------------|---------|
| `NL_DEMO_MODE` | `true` in demo compose | Enables auto-start + reset service |
| `NL_DEMO_CONFIG` | `demo.nle` | Sample rules bundled in the container |
| `NL_DEMO_RESET_INTERVAL_MINUTES` | `60` | Periodic reset; `0` = startup reset only |
| `NL_DEMO_STARTUP_DELAY_MS` | `750` | Delay before first auto-start |
| `NL_DEMO_BRIDGE_INTERVAL` | `8` | Seconds between event cycles in sidecar |

Phase E/F variables (`NL_PUBLIC_MODE`, `NL_BUS_TOKEN`, `NL_OPERATOR_KEY`, `NL_PUBLIC_*`) still apply.

## Deploy (production demo)

Same as Phase F — Phase G is enabled automatically in `docker-compose.demo.yml`:

```bash
cp docker/.env.demo.example docker/.env
# edit secrets + domain
bash scripts/deploy-demo.sh
```

Verify the loop:

```bash
curl -fsS https://YOUR_DOMAIN/api/v1/demo/status
# {"enabled":true,"sessionRunning":true,"decisions":12,...}
```

## Local / CI smoke (no Caddy)

```bash
export NL_BUS_TOKEN=$(openssl rand -hex 16)
export NL_OPERATOR_KEY=$(openssl rand -hex 16)
bash scripts/nl-demo-smoke.sh
```

Uses `docker/docker-compose.demo-local.yml` (session server + demo bridge only).

## Manual bridge (outside Docker)

If you run the session server locally with demo mode:

```bash
export NL_DEMO_MODE=true
export NL_PUBLIC_MODE=true
export NL_BUS_TOKEN=your-token
export NL_OPERATOR_KEY=your-op-key
dotnet run --project src/NL.SessionHost.Web
```

In another terminal:

```bash
pip install websocket-client
python integrations/python/nl_bridge.py \
  --url "ws://127.0.0.1:27021/nl/v1?token=your-token" \
  --loop --interval 8
```

## Demo reset behavior

On each reset cycle (startup + every `NL_DEMO_RESET_INTERVAL_MINUTES`):

1. Stop the running session (if any).
2. Truncate `moderation.jsonl` and reset `sp-profiles.json` to `[]`.
3. Reload moderation in-memory stores.
4. Apply the demo profile (`demo.nle` + session bus).
5. Start the session again.

The demo-bridge sidecar reconnects automatically if the session restarts.

## Related docs

- [NL Deploy — CI/CD & public demo (Phase F)](NL_DEPLOY.md)
- [NL Demo Security (Phase E)](NL_DEMO_SECURITY.md)
- [NL Integration Spec v1](NL_INTEGRATION_SPEC.md)
