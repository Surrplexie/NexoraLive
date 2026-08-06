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

## Platform sign-in (browser)

Open **`/identity-link.html`** to create an NL account and link platforms via OAuth/OpenID.

| Platform | Flow | Env |
|----------|------|-----|
| Steam | OpenID 2.0 | `STEAM_WEB_API_KEY` (live ownership) |
| Epic | OAuth 2.0 | `EPIC_CLIENT_ID`, `EPIC_CLIENT_SECRET` |
| Xbox | Microsoft OAuth + Xbox Live | `XBOX_CLIENT_ID`, `XBOX_CLIENT_SECRET` |
| PlayStation | PSN OAuth 2.0 | `PSN_CLIENT_ID`, `PSN_CLIENT_SECRET` |

### OAuth routes

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/identity/oauth/steam/authorize?accountId=…&returnUrl=…` | Steam OpenID |
| GET | `/api/v1/identity/oauth/steam/callback` | Steam callback |
| GET | `/api/v1/identity/oauth/epic/authorize?accountId=…&returnUrl=…` | Epic OAuth |
| GET | `/api/v1/identity/oauth/epic/callback` | Epic callback |
| GET | `/api/v1/identity/oauth/xbox/authorize?accountId=…&returnUrl=…` | Xbox OAuth |
| GET | `/api/v1/identity/oauth/xbox/callback` | Xbox callback |
| GET | `/api/v1/identity/oauth/playstation/authorize?accountId=…&returnUrl=…` | PlayStation OAuth |
| GET | `/api/v1/identity/oauth/playstation/callback` | PlayStation callback |
| GET | `/api/v1/identity/platform-oauth/{platform}/{accountId}` | Linked profile (no tokens) |

CSRF protection: short-lived `state` token in `identity/oauth-state.json`.

See [`samples/identity/platform-oauth.env.example`](../samples/identity/platform-oauth.env.example).

```powershell
powershell -File scripts/nl-identity-platform-oauth-validate.ps1
```

Expected: **`PLATFORM OAUTH VALIDATION PASSED`**

Manual linking (API / dev): `POST /api/v1/identity/link` still works without browser.

## Live ownership APIs (Phase L.3)

| Platform | API | Notes |
|----------|-----|-------|
| Steam | `IPlayerService/GetOwnedGames` | Requires `STEAM_WEB_API_KEY` |
| Epic | Ecom `POST /epic/ecom/v1/ownership` | Client credentials + linked Epic account id |
| Xbox | Title Hub batch lookup | Requires linked XUID + Xbox OAuth token |
| PlayStation | Entitlements API | Requires linked PSN account + user token |

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
Live Xbox/PlayStation: multiplayer access checked via Title Hub / entitlements when OAuth configured.

## Smoke test

```powershell
powershell -File scripts/nl-identity-smoke.ps1
powershell -File scripts/nl-identity-platform-oauth-validate.ps1
dotnet test tests/NL.Identity.Tests
```

Browser test: Session Host running → `/identity-link.html` → create account → link Steam/Epic/Xbox/PlayStation.
