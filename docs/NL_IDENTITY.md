# NL Identity — Phase L

Platform identity, game ownership verification, anti-alt linking, and **Steam OpenID** browser sign-in for NL session admission.

## Enable

```powershell
$env:NL_IDENTITY_ENABLED = "true"
$env:NL_OWNERSHIP_MODE = "mock"   # mock | live | off
$env:NL_PUBLIC_BASE_URL = "http://127.0.0.1:27020"  # OAuth callback base (required behind reverse proxy)
# Optional live Steam Web API (ownership + bans at admit):
# $env:STEAM_WEB_API_KEY = "..."
# $env:NL_STEAM_OPENID_REALM = "http://127.0.0.1:27020"
# $env:NL_IDENTITY_ENCRYPTION_KEY = "<32-byte-base64>"  # Linux token encryption
```

Copy `samples/identity/mock-ownership.json` to `%LOCALAPPDATA%\NL\identity\mock-ownership.json`
(or `$NL_DATA_ROOT/identity/`).

## Steam OpenID sign-in (browser)

1. Open **`/identity-link.html`** (or use **Sign in with Steam** on `/nl-client.html`)
2. **Create account** → receives `accountId`
3. **Sign in with Steam** → redirects to Steam → callback links Steam64 to account
4. Use linked Steam64 + `nlAccountId` on admit / join flow

### OAuth routes

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/identity/oauth/steam/authorize?accountId=…&returnUrl=…` | Start Steam OpenID redirect |
| GET | `/api/v1/identity/oauth/steam/callback` | Verify OpenID, link platform, redirect to `returnUrl` |

CSRF protection: short-lived `state` token in `identity/oauth-state.json`.

Manual linking (API / dev): `POST /api/v1/identity/link` still works without browser.

## Session profile (ownership required)

```json
{
  "requireGameOwnership": true,
  "gameId": "hello-fork",
  "platformAppId": "440",
  "ownershipPlatform": "steam"
}
```

See `samples/identity/session-profile-ownership.json`.

## Admit API (extended)

`POST /api/v1/session/admit`

```json
{
  "playerId": "alice",
  "displayName": "Alice",
  "platform": "steam",
  "platformUserId": "76561198000000001",
  "gameId": "hello-fork",
  "appId": "440",
  "nlAccountId": "nl-account-guid-from-identity-linker"
}
```

Response includes `ownershipStatus` on denial.

## Identity API

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/identity/settings` | Public mode + OAuth URLs |
| POST | `/api/v1/identity/accounts` | Create NL account |
| POST | `/api/v1/identity/link` | Link Steam/Epic/… manually |
| DELETE | `/api/v1/identity/link?accountId=…&platform=…&externalUserId=…` | Unlink platform |
| GET | `/api/v1/identity/accounts/{id}` | Account + links |
| GET | `/api/v1/identity/accounts/by-platform/{platform}/{externalUserId}` | Reverse lookup |
| GET | `/api/v1/identity/audit` | Recent audit events |

Returns **503** when `NL_IDENTITY_ENABLED=false`.

## Anti-alt rule

The same `platform + externalUserId` cannot link to two NL accounts. Admit checks this when `nlAccountId` is supplied.

## Publisher bans

Mock: `"banned": { "steam:…": true }`  
Live Steam: `ISteamUser/GetPlayerBans/v1` when `STEAM_WEB_API_KEY` is set.

## Console multiplayer pass

Mock: `subscriptionRequired` + `multiplayerActive` in `mock-ownership.json`.  
Live Xbox/PS enforcement requires platform SDK partnership (documented stub).

## Smoke test

```powershell
powershell -File scripts/nl-identity-smoke.ps1
dotnet test tests/NL.Identity.Tests
```

Browser test: Session Host running → `/identity-link.html` → create account → Sign in with Steam (requires reachable `NL_PUBLIC_BASE_URL` for callback).
