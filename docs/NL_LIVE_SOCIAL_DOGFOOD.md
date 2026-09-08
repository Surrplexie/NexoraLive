# Live Social Dogfood

End-to-end dogfood for **Phase M live social gate**: follow/sub/discord checks at admit, live-only NLS start, and NL Client join — using mock fixtures locally or real platform OAuth in live mode.

## Quick start (mock — no OAuth keys)

**Terminal 1** — start session host with social dogfood env:

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive
powershell -File scripts/nl-session-host-live-social-dogfood.ps1 -SocialMode mock
```

**Terminal 2** — run automated flow:

```powershell
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -SocialMode mock
```

Or use the operator UI: http://127.0.0.1:27020/live-social-dogfood.html

Expected: **`LIVE SOCIAL DOGFOOD FLOW PASSED`**

## What gets configured

| Asset | Path | Purpose |
|-------|------|---------|
| Join requirements | `{NL_DATA_ROOT}/join-requirements.json` | `requireFollow: true` |
| Streamer channels | `{NL_SOCIAL_ROOT}/streamer-social.json` | `dogfood-streamer` Twitch/Discord ids |
| Mock live + relationships | `{NL_SOCIAL_ROOT}/mock-social.json` | Live status + follower/stranger matrix |
| Session profile | `{NL_DATA_ROOT}/session-profile.json` | `socialGateEnabled`, `joinGate`, `requireLiveStream` |

### Mock players (`dogfood-streamer`)

| Player id | Follow | Sub | Discord |
|-----------|--------|-----|---------|
| `sp-dogfood-1` | yes | no | yes |
| `sp-dogfood-sub` | yes | yes | yes |
| `sp-dogfood-stranger` | no | no | no |

Steam64 for ownership mock: `76561198000000001`

## Live mode (real OAuth)

1. Copy env template:

```powershell
Copy-Item samples/social/live-social-dogfood.env.example docker/live-social-dogfood.env
# Edit: TWITCH_CLIENT_ID, TWITCH_CLIENT_SECRET, (+ Discord/YouTube/Kick as needed)
# Set redirect URIs to http://127.0.0.1:27020/api/v1/social/oauth/{platform}/callback
```

2. Optional streamer channel overrides:

```powershell
$env:DOGFOOD_TWITCH_BROADCASTER_ID = "your-broadcaster-id"
$env:DOGFOOD_DISCORD_GUILD_ID = "your-guild-id"
```

3. Start host:

```powershell
powershell -File scripts/nl-session-host-live-social-dogfood.ps1 -SocialMode live -EnvFile docker/live-social-dogfood.env
```

4. Link platforms at `/social-link.html` (player id = your SP / NL account id).

5. Run flow (follow checks use live APIs; live status still uses mock fixture until Twitch live API is wired for dogfood streamer):

```powershell
powershell -File scripts/nl-live-social-dogfood-flow.ps1 -SocialMode live
```

## REST API

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/v1/dogfood/setup` | Body: `{ "gameId": "hello-fork", "socialMode": "mock" }` |
| POST | `/api/v1/dogfood/social/setup` | Install social fixtures only |
| GET | `/api/v1/dogfood/status` | Includes social asset checklist |

## Validation

```powershell
powershell -File scripts/nl-live-social-dogfood-validate.ps1 -SocialMode mock
powershell -File scripts/nl-social-smoke.ps1
```

## User journeys

1. **Operator** — `/live-social-dogfood.html` → social setup → full setup → start session
2. **Follower SP** — `/nl-client.html` or join flow API with `sp-dogfood-1`
3. **Stranger SP** — admit denied (social/join gate)
4. **Live OAuth** — `/social-link.html` → `/join-gate.html` tune requirements → real admits

## Related docs

- [NL_SOCIAL_GATE.md](NL_SOCIAL_GATE.md) — Phase M architecture
- [NL_DOGFOOD_FLOW.md](NL_DOGFOOD_FLOW.md) — base fork dogfood (no social gate)
- [NL_PRODUCTION_DOGFOOD.md](NL_PRODUCTION_DOGFOOD.md) — production stack (social still mock by default)
