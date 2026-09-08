# NL Session Server (Phase D)

Phase D turns the local Session Host into a **networked NL Session Server**: remote game
bridges connect outbound, join eligibility is enforced **at connect time**, streamer `.nle`
rules run server-side, and moderation audit is centralized on the host.

`NL.SessionHost.Web` is the session server entry point (session bus + moderation + admission).

## Architecture

```text
  Game server (any OS)
       │
       │ 1. POST /api/v1/session/admit  (before allowing join)
       │ 2. ws://SESSION/nl/v1?token=…  (events in, actions out)
       ▼
  NL Session Server (:27020 HTTP, :27021 WS)
       │
       ├── RuleEngine (.nle server-side)
       ├── JoinEligibilityEngine (admit + playerJoin)
       ├── Anti-cheat signals
       └── Moderation audit (shared NL_DATA_ROOT)
```

Bridges do **not** write local NDJSON files. They connect to the hosted WebSocket and optionally
call the admit API before emitting `playerJoin`.

## Quick start

Terminal 1 — session server:

```bash
dotnet run --project src/NL.SessionHost.Web
```

Open `http://127.0.0.1:27020` for the operator dashboard. Copy the **remote bridge manifest**.

Terminal 2 — remote bridge (from game host or dev machine):

```bash
python integrations/python/nl_bridge.py \
  --url "ws://127.0.0.1:27021/nl/v1?token=YOUR_TOKEN" \
  --admit-url "http://127.0.0.1:27020/api/v1/session/admit" \
  --sample
```

Or run `scripts/nl-session-server-smoke.ps1`.

## Session manifest

`GET /api/v1/session/manifest` returns everything a remote integrator needs:

| Field | Purpose |
|-------|---------|
| `bridgeConnectUrl` | Outbound WebSocket for events + actions |
| `admitUrl` | Pre-connect join gate check |
| `moderationUrl` | Operator moderation console |
| `joinGateEnabled` | Whether profile has join gate on |
| `sessionRunning` | Whether rule engine is active |

## Join admission API

`POST /api/v1/session/admit`

```json
{
  "streamerId": "default-streamer",
  "playerId": "alice",
  "displayName": "Alice"
}
```

Response:

```json
{
  "decision": "Allow",
  "reason": "Allowed (default requirements met).",
  "playerId": "alice",
  "admit": true,
  "standing": "Normal"
}
```

| `admit` | Bridge should |
|---------|----------------|
| `true` | Allow player into game; emit `playerJoin` |
| `false` | Reject connect; do **not** emit `playerJoin` |

`Deny` and `Hold` both set `admit: false`. The in-session join gate still runs on `playerJoin`
events as a second line of defense.

Enable join gate in the session profile (dashboard checkbox or `"joinGate": true` in profile JSON).

## Public URLs (Docker / cloud)

When binding `0.0.0.0`, set public URL overrides so manifests show reachable addresses:

| Variable | Example | Purpose |
|----------|---------|---------|
| `NL_PUBLIC_HOST` | `nl.example.com` | Hostname for HTTP/WS URLs |
| `NL_PUBLIC_HTTP` | `https://nl.example.com` | Full HTTP override |
| `NL_PUBLIC_WS` | `wss://nl.example.com/nl/v1` | Full WebSocket override |
| `NL_PUBLIC_MOD_HTTP` | `https://mod.nl.example.com` | Moderation console URL |

## Docker (full session server stack)

```bash
docker compose -f docker/docker-compose.session-server.yml up --build
```

Set `NL_PUBLIC_HOST` in the compose file or environment when deploying behind a public IP/DNS.

## Security (Phase E)

Public demo deployments must set `NL_PUBLIC_MODE`, `NL_BUS_TOKEN`, and `NL_OPERATOR_KEY`.
See [NL_DEMO_SECURITY.md](NL_DEMO_SECURITY.md) and [`.env.example`](../.env.example).

## REST surface (session server)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/security` | Public | Auth mode info |
| `GET` | `/api/v1/session/manifest` | Public* | Remote bridge manifest (*token redacted) |
| `POST` | `/api/v1/session/admit` | Public | Pre-connect join admission |
| `GET` | `/api/v1/session` | Public* | Session status (*secrets redacted) |
| `PUT` | `/api/v1/session/profile` | Operator | Session profile |
| `POST` | `/api/v1/session/start` | Operator | Start rule engine |
| `POST` | `/api/v1/session/stop` | Operator | Stop session |
| `GET` | `/api/v1/moderation/recent` | Public | Audit tail (read) |
| `POST` | `/api/v1/moderation/*` | Operator | Moderation actions |
| `GET` | `/health` | Public | Health check |

## Bridge integration checklist

1. Fetch manifest from session server (or configure URLs manually).
2. On each player connect: `POST admitUrl` with `playerId`.
3. If `admit: false`, reject the player in your game server.
4. Open WebSocket to `bridgeConnectUrl` (reconnect loop).
5. Emit semantic NDJSON events; handle inbound action lines.
6. On Block for `playerJoin`, kick the player (NL also blocks via join gate).

## Related docs

- [NL Session Server (Phase D)](NL_SESSION_SERVER.md)
- [NL Demo Security (Phase E)](NL_DEMO_SECURITY.md)
- [Headless Linux / Docker](NL_HEADLESS_LINUX.md)
- [SP model & join eligibility](SP_MODEL.md)
