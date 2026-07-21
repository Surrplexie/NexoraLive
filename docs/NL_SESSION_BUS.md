# NL Session Bus (Phase B)

The **session bus** is a hosted control plane for one NL session: a cross-platform web dashboard,
REST API, and authenticated WebSocket bridge socket. Game mods connect to the bus instead of
managing file paths or ad-hoc ports.

## Components

| Piece | Default | Role |
|-------|---------|------|
| `NL.SessionHost.Web` | `http://127.0.0.1:27020` | Dashboard + REST API |
| WebSocket listener | `ws://127.0.0.1:27021/nl/v1` | Bidirectional bridge (events in, actions out) |
| `NL.SessionHost` (WinForms) | Windows | Same session runner; optional local bus mode |
| `SessionHostService` | shared | Start/stop + log buffer for web + WinForms |

## Quick start

Terminal 1 — web Session Host:

```bash
dotnet run --project src/NL.SessionHost.Web
```

Note the printed **bridge URL** (includes `?token=`) and open `http://127.0.0.1:27020`.

Terminal 2 — reference bridge:

```bash
python integrations/python/nl_bridge.py --url "ws://127.0.0.1:27021/nl/v1?token=YOUR_TOKEN" --sample
```

Or run `scripts/nl-session-bus-smoke.ps1` (starts web host + Python bridge).

## Environment variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `NL_BIND` | `127.0.0.1` | HTTP + WebSocket bind address |
| `NL_HTTP_PORT` | `27020` | Dashboard / REST |
| `NL_WS_PORT` | `27021` | Bridge WebSocket |
| `NL_BUS_TOKEN` | random | Required `?token=` on bridge connects |

## REST API

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/bus` | Bus connection info (URLs, token, session id) |
| `GET` | `/api/v1/session` | Status, profile, log tail, decision count |
| `PUT` | `/api/v1/session/profile` | Save session profile JSON |
| `POST` | `/api/v1/session/bus-defaults` | Apply generic + WebSocket source + `nl-action auto` |
| `POST` | `/api/v1/session/start` | Start session (`{ "replayOnce": false }`) |
| `POST` | `/api/v1/session/stop` | Stop session |

Profile fields include `useSessionBus`, `nlActionEndpoint`, and `busToken`. When
`useSessionBus` is true, NL listens on the WebSocket port and validates bridge tokens.

## WinForms (Windows)

`NL.SessionHost` supports the same profile fields:

- **Source** — file path, `tcp://`, or `ws://127.0.0.1:27021/nl/v1`
- **NL action** — `auto` (paired WebSocket) or `tcp://host:port`
- **Use session bus** — sets WebSocket source + token for bridge auth

Tools → **Load session bus defaults** fills a generic profile wired for local bus ports.

## Bridge templates

See [`integrations/README.md`](../integrations/README.md) for Python, Node, Lua, Unity, Unreal,
Godot, Rust, and .NET starters. Pass the full `bridgeConnectUrl` from the dashboard (token included).

## Related docs

- [NL Game Integration Spec v1](NL_INTEGRATION_SPEC.md) — event/action NDJSON contract
- [ROADMAP.md](../ROADMAP.md) — Phase A (protocol) + Phase B (session bus) + Phase C (headless Linux)
