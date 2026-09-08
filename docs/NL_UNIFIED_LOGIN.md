# NL Unified Login & Accounts

One NL account binds **identity**, **SP player profile**, and optional **streamer** capability. Users sign in once and use NL Client, platform linking, verification, and join flows without juggling separate ids.

## Model

| Layer | Id | Storage |
|-------|-----|---------|
| NL Identity | `accountId` | `identity/accounts.json` |
| SP player | `playerId` (= `accountId`) | `sp-profiles.json` |
| Streamer | `streamerId` (slug or account id) | `social/streamer-social.json` |

Capabilities (`NlAccountCapabilities`):

- **Player** — always (SP profile + social links)
- **Streamer** — after `POST /api/v1/auth/streamer/enable`

## Quick start

```powershell
# Register + login via UI
# Open http://127.0.0.1:27020/login.html
```

Or API:

```powershell
# Register
Invoke-RestMethod -Method POST -Uri http://127.0.0.1:27020/api/v1/auth/register `
  -ContentType application/json `
  -Body '{"displayName":"Alice","email":"alice@example.com","password":"password123"}'

# Login (save sessionToken)
Invoke-RestMethod -Method POST -Uri http://127.0.0.1:27020/api/v1/auth/login `
  -ContentType application/json `
  -Body '{"email":"alice@example.com","password":"password123"}'

# Authenticated requests
$headers = @{ Authorization = "Bearer $sessionToken" }
Invoke-RestMethod -Uri http://127.0.0.1:27020/api/v1/auth/me -Headers $headers
```

## REST API

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/auth/register` | Create account + SP profile + session |
| POST | `/api/v1/auth/login` | Email/password login (+ optional 2FA code) |
| POST | `/api/v1/auth/logout` | Revoke session |
| GET | `/api/v1/auth/me` | Current account summary + verification + links |
| POST | `/api/v1/auth/streamer/enable` | Enable streamer slug + social config |

Session auth: `Authorization: Bearer <token>` or `X-NL-Session-Token`.

## Automatic session use

When signed in, these endpoints auto-fill `nlAccountId` and `playerId` from the session:

- `POST /api/v1/session/admit`
- `POST /api/v1/client/join-flow`

## User journeys

1. **Player** — Register → link platforms at `/identity-link.html` → verify at `/account-verify.html` → join via `/nl-client.html`
2. **Streamer** — Same account → **Enable streamer mode** on login page → configure join gate → go live via operator tools
3. **Both** — One login covers SP joins to other streamers and operating your own streamer id

## Legacy compatibility

- `POST /api/v1/identity/accounts` creates identity accounts and provisions the SP player profile (`playerId` = `accountId`); no password or session
- Manual `nlAccountId` / `playerId` on admit still works without session

## Validation

```powershell
powershell -File scripts/nl-unified-login-validate.ps1
```

Expected: **`UNIFIED NL LOGIN VALIDATION PASSED`**
