# NL Demo Hardening (Phase K)

Phase K keeps the public demo **safe and stable** under random traffic: HTTP rate limits, WebSocket connection caps, enriched health/ops probes, demo session bounds, edge security headers, and CI smoke for 429 behavior.

## Components

| Layer | What it does |
|-------|----------------|
| **HTTP rate limits** | Middleware on admit + public read endpoints |
| **WebSocket guard** | Max connections, per-IP cap, connect rate limit |
| **Ops metrics** | `/health` + `/api/v1/ops/status` for monitoring |
| **Session bounds** | Optional max demo session duration before auto-restart |
| **Caddy headers** | Security headers at TLS edge |
| **CI smoke** | `scripts/nl-hardening-smoke.sh` verifies 429 |

## HTTP rate limiting

Enabled when `NL_HARDENING=true` (default **on** if `NL_PUBLIC_MODE=true`).

| Endpoint group | Default limit | Paths |
|----------------|---------------|-------|
| Admit | 120/min/IP | `POST /api/v1/session/admit` |
| Public read | 300/min/IP | `/api/v1/spectator/*`, `/api/v1/demo/status`, `/api/v1/ops/status`, moderation read APIs |

Exceeded limits return **429** with `Retry-After: 60`.

Spectator triggers (`POST /api/v1/spectator/trigger`) keep their separate limiter from Phase H (`NL_SPECTATOR_TRIGGER_RATE_PER_MIN`).

## WebSocket bridge limits

Applied at upgrade time on `/nl/v1`:

- **Global cap** — `NL_WS_MAX_CONNECTIONS` (default 16)
- **Per-IP cap** — `NL_WS_MAX_CONNECTIONS_PER_IP` (default 4)
- **Connect rate** — `NL_WS_CONNECT_RATE_PER_MIN` (default 30)

Rejected upgrades return HTTP **429** before the WebSocket handshake completes.

## Monitoring endpoints

### `GET /health`

Extended probe for Docker/Kubernetes:

```json
{
  "status": "ok",
  "service": "nl-session-server",
  "uptimeSeconds": 3600,
  "publicMode": true,
  "hardening": true,
  "demoMode": true,
  "sessionRunning": true
}
```

### `GET /api/v1/ops/status`

Public read-only ops dashboard (no secrets):

```json
{
  "uptime": { "startedUtc": "...", "uptimeSeconds": 3600 },
  "hardening": { "enabled": true, "admitRatePerMinute": 120, ... },
  "rateLimits": { "admitRejected": 0, "publicReadRejected": 0 },
  "webSocket": { "activeConnections": 1, "rejected": 0, ... },
  "demo": { "enabled": true, "sessionRunning": true, "decisions": 42 },
  "session": { "state": "Running", "decisions": 42 }
}
```

## Demo session max duration

When `NL_DEMO_MODE=true` and `NL_DEMO_SESSION_MAX_HOURS` > 0 (default **12**), the demo hosted service restarts the session after that many hours — same path as periodic data reset (stop → clear data → start).

Set `NL_DEMO_SESSION_MAX_HOURS=0` to disable.

## Environment variables

```env
NL_HARDENING=true
NL_ADMIT_RATE_PER_MIN=120
NL_PUBLIC_READ_RATE_PER_MIN=300
NL_WS_MAX_CONNECTIONS=16
NL_WS_MAX_CONNECTIONS_PER_IP=4
NL_WS_CONNECT_RATE_PER_MIN=30
NL_DEMO_SESSION_MAX_HOURS=12
```

## Edge security (Caddy)

The demo `Caddyfile` adds:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`

## Verify locally

```bash
export NL_BUS_TOKEN=$(openssl rand -hex 16)
export NL_OPERATOR_KEY=$(openssl rand -hex 16)
export NL_ADMIT_RATE_PER_MIN=5
bash scripts/nl-hardening-smoke.sh
```

## Related docs

- [NL Demo Runbook](NL_DEMO_RUNBOOK.md)
- [NL Demo Security (Phase E)](NL_DEMO_SECURITY.md)
- [NL Deploy (Phase F)](NL_DEPLOY.md)
