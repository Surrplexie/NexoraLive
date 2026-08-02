# NL Identity — Phase L

Platform identity, game ownership verification, and anti-alt linking for NL session admission.

## Enable

```powershell
$env:NL_IDENTITY_ENABLED = "1"
$env:NL_OWNERSHIP_MODE = "mock"   # mock | live | off
# Optional live Steam:
# $env:STEAM_WEB_API_KEY = "..."
# $env:NL_IDENTITY_ENCRYPTION_KEY = "<32-byte-base64>"  # Linux token encryption
```

Copy `samples/identity/mock-ownership.json` to `%LOCALAPPDATA%\NL\identity\mock-ownership.json`
(or `$NL_DATA_ROOT/identity/`).

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
  "nlAccountId": "optional-nl-account-guid"
}
```

Response includes `ownershipStatus` on denial.

## Identity API

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/identity/settings` | Public mode info |
| POST | `/api/v1/identity/accounts` | Create NL account |
| POST | `/api/v1/identity/link` | Link Steam/Epic/… (one per platform globally) |
| GET | `/api/v1/identity/accounts/{id}` | Account + links |
| GET | `/api/v1/identity/audit` | Recent audit events |

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
```
