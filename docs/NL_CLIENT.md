# NL Client shell (Phase R)

Cross-platform join shell: pick streamer → ownership proof → admit → connect manifest → launch params.

## Quick start

**Web client:** `http://127.0.0.1:27020/nl-client.html`  
**CLI:**

```powershell
dotnet run --project src/NL.Client -- join --player sp-demo-1 --streamer default-streamer --platform-user 76561198000000001 --ack
```

**Deep link:**

```
nlclient://join?streamer=default-streamer&game=hello-fork&major=1.0
```

```powershell
dotnet run --project src/NL.Client -- deeplink --url "nlclient://join?streamer=default-streamer&game=hello-fork&major=1.0" --platform-user 76561198000000001 --ack
```

## Join flow steps

1. Browse live streamers (`GET /api/v1/client/streamers`)
2. Load session manifest — verify session running + partnership tier
3. At-own-risk acknowledgment when required (Phase Q)
4. Ownership proof via `platformUserId` (Phase L)
5. `POST /api/v1/session/admit`
6. Receive launch params (`bridgeConnectUrl`, `forkConnectEndpoint`)

`POST /api/v1/client/join-flow` runs the full pipeline server-side.

## Modes

| Mode | Description |
|------|-------------|
| `Player` | Standard SP join UX |
| `Streamer` | Elevated shell (session controls via operator console) |
| `MobileCompanion` | Subset: warn/kick via `/nl-client-mobile.html` |

## Block stray invites

Native multiplayer invites pointing at NL session endpoints are rejected:

```powershell
dotnet run --project src/NL.Client -- block-invite --url "http://127.0.0.1:27020/api/v1/session/admit"
```

`POST /api/v1/client/block-invite` — same logic for web client.

## In-session overlay

`GET /api/v1/client/overlay/{playerId}?streamer=…` returns standing, active offenses, recent warnings, clip trigger availability.

## Mobile companion (subset)

`/nl-client-mobile.html` — mod warn/kick actions via `POST /api/v1/client/mobile/action`.

Full mobile admin deferred to later phases.

## REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/client/settings` | Client capabilities |
| GET | `/api/v1/client/streamers` | Live streamer list |
| POST | `/api/v1/client/join-flow` | Full join pipeline |
| POST | `/api/v1/client/launch-params` | Build game launch command |
| POST | `/api/v1/client/block-invite` | Stray invite guard |
| GET | `/api/v1/client/overlay/{playerId}` | SP overlay state |
| POST | `/api/v1/client/mobile/action` | Mobile warn/kick |

## Environment

```bash
NL_CLIENT_SESSION_URL=http://127.0.0.1:27020
NL_OPERATOR_KEY=optional-for-manifest-secrets
```

## Tests

```powershell
dotnet test tests/NL.Client.Tests -c Release
./scripts/nl-client-smoke.ps1
```

## Exit criteria

SP opens NL Client → joins live streamer's fork session with ownership verified end-to-end.
