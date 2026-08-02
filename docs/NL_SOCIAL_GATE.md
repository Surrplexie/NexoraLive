# NL Social Gate (Phase M)

Phase M wires `JoinEligibilityEngine` to **live platform APIs** (or mock fixtures) so follow,
subscription, and Discord membership are verified at admit time — not caller-supplied booleans.

## Quick start (mock mode)

```powershell
$env:NL_SOCIAL_ENABLED = "true"
$env:NL_SOCIAL_MODE = "mock"
Copy-Item samples/social/mock-social.json "$env:LOCALAPPDATA/NL/social/mock-social.json"
Copy-Item samples/social/streamer-social.json "$env:LOCALAPPDATA/NL/social/streamer-social.json"
Copy-Item samples/social/join-requirements-follow.json "$env:LOCALAPPDATA/NL/join-requirements.json"
```

Open **Join gate** UI: `http://127.0.0.1:27020/join-gate.html`

## Architecture

```
Admit request → SocialGateService (live API / mock)
             → updates SpStreamerRelationship (follow/sub/discord)
             → JoinEligibilityEngine
             → Ownership gate (Phase L, optional)
             → Allow / Deny / Hold
```

| Component | Role |
|-----------|------|
| `NL.Social.Core` | `ISocialRelationshipProvider`, `ILiveStreamMonitor`, models |
| `NL.Social` | Mock + Twitch Helix providers, JSON stores, cache |
| `SocialGateService` | Hydrates SP profile before join eligibility |
| `NlLiveOnlyHostedService` | Auto-stops NLS when stream ends |
| `/join-gate.html` | Operator UI for requirements + streamer channels |

## Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `NL_SOCIAL_ENABLED` | off | Enable social gate + live monitor |
| `NL_SOCIAL_MODE` | `mock` | `off`, `mock`, or `live` |
| `NL_SOCIAL_ROOT` | `%LOCALAPPDATA%/NL/social` | Config + link store root |
| `NL_SOCIAL_MOCK_DATA` | `{root}/mock-social.json` | Fixture for mock mode |
| `NL_SOCIAL_CACHE_TTL_SEC` | `300` | Relationship/live cache TTL |
| `NL_LIVE_CHECK_INTERVAL_SEC` | `60` | Poll interval for auto-stop |
| `TWITCH_CLIENT_ID` | — | Required for `live` mode Twitch checks |
| `TWITCH_ACCESS_TOKEN` | — | Broadcaster/mod token with follow/sub scopes |

## REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/social/settings` | Public social config info |
| GET/PUT | `/api/v1/social/join-requirements` | Streamer join rules |
| GET/PUT | `/api/v1/social/streamer-config` | Connected channels |
| GET | `/api/v1/social/live-status?streamer=` | Current live state |
| POST | `/api/v1/social/link` | Link platform ids to SP |
| GET | `/api/v1/social/links/{playerId}` | Read SP social links |

Admit body extensions (`POST /api/v1/session/admit`):

```json
{
  "playerId": "follower-sp",
  "twitchUserId": "111",
  "discordUserId": "222"
}
```

## Live-only NLS

When `requireLiveStream` is set on the session profile (via Join gate UI or profile JSON),
`POST /api/v1/session/start` returns **400** if the streamer is offline. While running,
`NlLiveOnlyHostedService` polls live status and stops the session when the stream ends.

Mock live state comes from `mock-social.json` → `liveStatus[streamerId].isLive`.

## Native invite blocking

nl.txt section 2: SPs join **only through NL**, not native game platform invites. NLS
addresses are NL-managed; traffic that bypasses the join admission layer is rejected by
design. Game bridges should connect outbound to the NL session bus — there is no supported
path for a raw platform invite to reach an NL-gated session.

Document this for streamers: share the NL manifest admit URL, not a raw server IP.

## Offense archive

Offenses remain in storage forever but only count toward join eligibility for **2 years**
(`SpOffense.ActiveWindow`). The moderation API returns `activeOffenses` and
`archivedOffenses` separately; the web console shows both with an Active/Archived column.

## OAuth note

Full Twitch/YouTube/Kick **browser OAuth** flows are deferred (same pattern as Phase L Steam
OpenID). Production deployments can supply refresh tokens via `/api/v1/social/link` or env
tokens for Helix until OAuth UI ships.

## Smoke test

```powershell
./scripts/nl-social-smoke.ps1
```
