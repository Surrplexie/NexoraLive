# NL Spectator UX (Phase H)

Phase H separates **watching** the public demo from **operating** it. Visitors get a read-only landing page with a live decision feed and optional rule triggers; operators keep full control behind `NL_OPERATOR_KEY`.

## Pages

| URL | Audience | Purpose |
|-----|----------|---------|
| `/` | Public | Spectator landing — live stats, decision feed, try-a-rule buttons |
| `/operator.html` | Operator | Session control, bridge manifest, secrets, live log |
| `/moderation.html` | Both | Audit trail (read); mod writes require operator key |

## Public API (no operator key)

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/v1/spectator/status` | Session running, decision count, demo/trigger flags |
| `GET` | `/api/v1/spectator/decisions` | Automatic Allow/Block/Warn feed (`?since=`, `?count=`) |
| `GET` | `/api/v1/spectator/scenarios` | Preset events visitors can trigger |
| `POST` | `/api/v1/spectator/trigger` | Inject one preset event into the live session (rate-limited) |

Existing public reads still work: `/health`, `/api/v1/demo/status`, `/api/v1/moderation/recent`.

## Operator gate

When `NL_PUBLIC_MODE=true` (or `NL_OPERATOR_KEY` is set):

- **Write endpoints** require `X-NL-Operator-Key` (unchanged from Phase E).
- **`GET /api/v1/session`** redacts profile paths, RCON, bus token, and session log for unauthenticated callers.
- **Operator console** hides manifest/profile/log panels until a key is saved in the browser.

Bridge tokens and connect URLs remain redacted in manifest/bus responses unless the caller is operator-authenticated.

## Spectator triggers

Visitors can fire preset scenarios from `/` (e.g. shoot → Block, ALL CAPS chat → Block).

| Variable | Default | Purpose |
|----------|---------|---------|
| `NL_SPECTATOR_TRIGGERS` | `true` | Enable/disable `POST /api/v1/spectator/trigger` |
| `NL_SPECTATOR_TRIGGER_RATE_PER_MIN` | `12` | Max triggers per client IP per minute |
| `NL_SPECTATOR_FEED_MAX` | `100` | Max decisions returned per feed request |

Triggers inject events by opening a short-lived internal WebSocket client to the session bus (same path as game bridges). Requires an active session (`NL_DEMO_MODE` demo loop satisfies this).

## Local development

```bash
dotnet run --project src/NL.SessionHost.Web
# Open http://127.0.0.1:27020/ — spectator landing
# Open http://127.0.0.1:27020/operator.html — full console (no key in local dev)
```

With demo mode + bridge:

```bash
export NL_DEMO_MODE=true NL_BUS_TOKEN=dev NL_OPERATOR_KEY=dev
dotnet run --project src/NL.SessionHost.Web
python integrations/python/nl_bridge.py --url "ws://127.0.0.1:27021/nl/v1?token=dev" --loop
```

## Related docs

- [NL Hosted Demo Loop (Phase G)](NL_DEMO.md)
- [NL Demo Security (Phase E)](NL_DEMO_SECURITY.md)
