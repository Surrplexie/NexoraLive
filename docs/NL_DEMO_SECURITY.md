# NL Demo Security (Phase E)

Phase E hardens `NL.SessionHost.Web` and `NL.Moderation.Web` for **public internet exposure**.
Local development (`127.0.0.1`, no secrets) keeps the previous permissive behavior.

## Quick reference

| Variable | Required when | Purpose |
|----------|---------------|---------|
| `NL_PUBLIC_MODE` | Public demo | Enables strict startup checks + CORS tightening |
| `NL_BUS_TOKEN` | `NL_PUBLIC_MODE=true` | Fixed WebSocket bridge token (`?token=` on `/nl/v1`) |
| `NL_OPERATOR_KEY` | `NL_PUBLIC_MODE=true` | Protects session control + moderation writes |
| `NL_CORS_ORIGINS` | Public cross-origin UI | Comma-separated allowed browser origins |
| `NL_PUBLIC_HTTP` | Behind TLS proxy | Default CORS origin when `NL_CORS_ORIGINS` unset |

Copy [`.env.example`](../.env.example) and set real secrets before deploying.

## Authentication model

Two credentials serve different roles:

| Credential | Used by | Protects |
|------------|---------|----------|
| **`NL_BUS_TOKEN`** | Game bridges (WebSocket) | Event stream + inbound actions on `/nl/v1` |
| **`NL_OPERATOR_KEY`** | Operators (REST + web UI) | Session start/stop, profile edits, bans/warns |

### Operator REST auth

Send on protected write requests:

```http
X-NL-Operator-Key: your-operator-key
```

Or:

```http
Authorization: Bearer your-operator-key
```

The web dashboards show an **Operator key** panel when auth is required. The key is stored in
`sessionStorage` for the browser tab only.

### Protected write endpoints

| Method | Path |
|--------|------|
| `PUT` | `/api/v1/session/profile` |
| `POST` | `/api/v1/session/bus-defaults` |
| `POST` | `/api/v1/session/start` |
| `POST` | `/api/v1/session/stop` |
| `POST` | `/api/v1/moderation/profiles` |
| `POST` | `/api/v1/moderation/warning` |
| `POST` | `/api/v1/moderation/ban` |
| `POST` | `/api/v1/moderation/graylist` |
| `POST` | `/api/v1/moderation/clear` |

### Public read endpoints (no operator key)

- `GET /health`
- `GET /api/v1/security` — reports whether operator auth is active
- `GET /api/v1/session/manifest` — bridge URLs **redacted** unless operator-authenticated
- `GET /api/v1/session` — session status (bus token redacted unless authenticated)
- `POST /api/v1/session/admit` — join gate probe (game bridges; intentionally open)
- `GET /api/v1/moderation/recent` — audit tail (read-only)
- `GET /api/v1/moderation/players/{id}/history` — offense history (read-only)
- `GET /api/v1/demo/status` — Phase G demo loop status
- `GET /api/v1/spectator/status` — Phase H public session summary
- `GET /api/v1/spectator/decisions` — live Allow/Block/Warn feed (automatic decisions only)
- `GET /api/v1/spectator/scenarios` — preset try-a-rule scenarios
- `POST /api/v1/spectator/trigger` — inject one preset event (rate-limited; session must be running)

### Spectator read vs operator session status

`GET /api/v1/session` remains public but **redacts** profile file paths, RCON, bus token, and the session log unless the request includes a valid operator key. Use `/api/v1/spectator/*` for the public demo UI.

See [NL Spectator UX (Phase H)](NL_SPECTATOR.md).

## Public mode startup

When `NL_PUBLIC_MODE=true`, the process **refuses to start** unless both `NL_BUS_TOKEN` and
`NL_OPERATOR_KEY` are set. This prevents accidentally exposing an ephemeral bus token or an
unauthenticated moderation API.

Local dev (default):

```bash
dotnet run --project src/NL.SessionHost.Web
# Operator auth: off · Bus token: random per run
```

Public demo:

```bash
export NL_PUBLIC_MODE=true
export NL_BUS_TOKEN="$(openssl rand -hex 32)"
export NL_OPERATOR_KEY="$(openssl rand -hex 32)"
export NL_BIND=0.0.0.0
export NL_PUBLIC_HTTP=https://demo.example.com
export NL_CORS_ORIGINS=https://demo.example.com
dotnet run --project src/NL.SessionHost.Web
```

## CORS

| Mode | Behavior |
|------|----------|
| Local dev | All origins allowed (same as before) |
| Public + `NL_CORS_ORIGINS` | Only listed origins |
| Public + `NL_PUBLIC_HTTP` only | That origin allowed |
| Public + neither | Cross-origin browser calls blocked |

Same-origin dashboard requests (operator UI served from the session server) do not need CORS.

## Docker

See comments in `docker/docker-compose.session-server.yml`. Set secrets via environment or an
`.env` file — never commit real keys.

## Testing operator auth locally

Enable auth without full public mode:

```bash
export NL_OPERATOR_KEY=dev-op-key
dotnet run --project src/NL.SessionHost.Web
```

Writes return `401` until you pass the header. Reads still work.

## Related docs

- [NL Deploy — CI/CD & public demo (Phase F)](NL_DEPLOY.md)
- [NL Session Server (Phase D)](NL_SESSION_SERVER.md)
- [Headless Linux / Docker](NL_HEADLESS_LINUX.md)
