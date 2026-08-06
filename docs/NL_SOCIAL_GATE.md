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
| `TWITCH_CLIENT_SECRET` | — | Required for Twitch OAuth player linking |
| `TWITCH_ACCESS_TOKEN` | — | Optional legacy server token when players have not OAuth-linked |
| `DISCORD_CLIENT_ID` | — | Required for Discord OAuth player linking |
| `DISCORD_CLIENT_SECRET` | — | Required for Discord OAuth player linking |
| `NL_PUBLIC_BASE_URL` | request host | OAuth callback base (must match app redirect URIs) |

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

## Twitch OAuth (Phase M.1)

Players link their own Twitch account via browser OAuth — no manual id entry required.

1. Open **`/social-link.html`**
2. Enter **player id** (NL SP profile id)
3. Click **Sign in with Twitch**
4. On callback, Twitch user id + encrypted refresh token are stored; follow/sub checks use the player's token at admit

### OAuth routes

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/social/oauth/twitch/authorize?playerId=…&returnUrl=…` | Start Twitch OAuth redirect |
| GET | `/api/v1/social/oauth/twitch/callback` | Exchange code, link account, redirect |
| GET | `/api/v1/social/twitch-oauth/{playerId}` | Read linked Twitch profile (no tokens) |

Configure Twitch developer app redirect URI:

`{NL_PUBLIC_BASE_URL}/api/v1/social/oauth/twitch/callback`

See [`samples/social/twitch-oauth.env.example`](../samples/social/twitch-oauth.env.example).

```powershell
powershell -File scripts/nl-social-twitch-oauth-validate.ps1
```

Expected: **`TWITCH OAUTH VALIDATION PASSED`**

## Discord OAuth (Phase M.2)

Players link Discord via browser OAuth; guild membership is verified live at admit using the player's token.

1. Open **`/social-link.html`**
2. Enter **player id**
3. Click **Sign in with Discord**
4. Set streamer **Discord guild id** in `/join-gate.html`
5. At admit, `GET /users/@me/guilds/{guild.id}/member` confirms membership

### OAuth routes

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/social/oauth/discord/authorize?playerId=…&returnUrl=…` | Start Discord OAuth redirect |
| GET | `/api/v1/social/oauth/discord/callback` | Exchange code, link account, redirect |
| GET | `/api/v1/social/discord-oauth/{playerId}` | Read linked Discord profile (no tokens) |

Configure Discord app redirect URI:

`{NL_PUBLIC_BASE_URL}/api/v1/social/oauth/discord/callback`

Scopes: `identify guilds.members.read`

See [`samples/social/discord-oauth.env.example`](../samples/social/discord-oauth.env.example).

```powershell
powershell -File scripts/nl-social-discord-oauth-validate.ps1
```

Expected: **`DISCORD OAUTH VALIDATION PASSED`**

## Smoke test

```powershell
./scripts/nl-social-smoke.ps1
```

## Native invite blocking
