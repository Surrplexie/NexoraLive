# NL Publisher & Platform Partnerships (Phase Q)

Legal and product layer for **Official**, **Platform**, and **At own risk** fork integrations.

## Quick start

```powershell
$env:NL_PARTNERSHIP_ENABLED = "true"
$env:NL_FORK_CATALOG_ENABLED = "true"
Copy-Item samples/partnership/publishers.json "$env:LOCALAPPDATA/NL/partnership/publishers.json"
Copy-Item samples/partnership/platform-opt-in.json "$env:LOCALAPPDATA/NL/partnership/platform-opt-in.json"
Copy-Item samples/fork/catalog.json "$env:LOCALAPPDATA/NL/fork-catalog/catalog.json"
dotnet run --project src/NL.SessionHost.Web
```

- **Publisher dashboard:** `/partnership.html`
- **At-own-risk SP flow:** `/at-own-risk-ack.html`

## Partnership tiers (from catalog)

| Tier | Ack required | UI copy |
|------|--------------|---------|
| `Official` | No | Publisher-approved NL session |
| `Platform` | No | Platform-opted title |
| `AtOwnRisk` | **Yes** (once per player per gameId) | Not endorsed; no progress transfer |

Catalog tier drives manifest `partnershipTier`, legal URLs, and admit gate behavior.

## At-own-risk flow

1. SP calls `GET /api/v1/partnership/legal/{gameId}` — session disclaimer + EULA notices
2. First join on `AtOwnRisk` title without prior ack → admit returns `requiresAtOwnRiskAcknowledgment: true` (Hold)
3. SP posts `POST /api/v1/partnership/acknowledge` **or** sends `atOwnRiskAcknowledged: true` on admit
4. Subsequent joins skip the banner for that player + gameId + disclaimer version

Partnered titles (`Official` / `Platform`) skip the at-own-risk banner.

## Play on NL SDK (publisher integration spec)

`GET /api/v1/partnership/sdk/spec` returns the integration contract:

- **Ownership token** — `POST /api/v1/partnership/sdk/ownership-token` (stub)
- **Fork auth** — session manifest + admit URLs
- **Disclaimer** — legal bundle + acknowledge endpoint
- **Menu entry** — in-game "Play on NL" button spec (publisher implements)
- **Deep link** — `nlclient://join?streamer=…&game=…&major=…`

Publishers implement the menu button; NL Client (Phase R) handles the deep link.

## Platform-wide opt-in

Steam (or other platform) app id → NL fork hosting:

```json
{ "platform": "steam", "appId": "480", "gameId": "hello-fork", "tier": "Platform", "enabled": true }
```

When admit includes matching `platform` + `appId`, tier may upgrade from catalog default.

## Publisher dashboard (placeholder)

| Endpoint | Description |
|----------|-------------|
| GET | `/api/v1/partnership/publishers` |
| POST | `/api/v1/partnership/publishers/register` |
| PUT | `/api/v1/partnership/publishers/{id}/titles/{gameId}` — opt in/out |
| GET | `/api/v1/partnership/dashboard/{publisherId}` — join counts, ban counts |

No revenue-share tooling — NL does not sell game content.

## Ban sync webhooks

Partnered publishers push deny-list updates:

```http
POST /api/v1/partnership/ban-sync
X-NL-Partnership-Secret: <NL_PARTNERSHIP_WEBHOOK_SECRET>
Content-Type: application/json

{ "action": "ban", "gameId": "hello-fork", "platformUserId": "76561198000000000", "reason": "Publisher ban" }
```

Actions: `ban` / `unban` (aliases `add` / `remove`).

## EULA / ToS templates

Static notices in `PartnershipLegalTemplates`:

- Session disclaimer (tier-specific)
- No progress transfer
- No NL sale of DLC / game copies

Override per catalog row via `legalNotice` on `ForkCatalogEntry`.

## Environment

```bash
NL_PARTNERSHIP_ENABLED=true
NL_PARTNERSHIP_GATE_ADMIT=1
NL_PARTNERSHIP_WEBHOOK_SECRET=change-me
NL_PARTNERSHIP_ROOT=/data/partnership
```

## Tests

```powershell
dotnet test tests/NL.Partnership.Tests -c Release
./scripts/nl-partnership-smoke.ps1
```

## Exit criteria

Catalog tier drives UI copy + legal gate; partnered title skips at-own-risk banner; unpartnered requires acknowledgment once per user per title.
