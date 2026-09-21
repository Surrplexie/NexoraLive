# Path A session log — `play.20062006.xyz`

Operator log for public NL dogfood. Staging only — not GA marketing.

| Field | Value |
|-------|--------|
| Host | `https://play.20062006.xyz` |
| VPS | OVHcloud US VPS-2 · `40.160.88.114` · `/opt/NexoraLive` |
| Streamer | `surrplexie-7c0056` |
| Game | `rimworld` · config `rimworld.nle` |
| Kill metric | Session 1 + **session 2** before Cities / new titles |

Related: [PATH_A_PROOF.md](PATH_A_PROOF.md) · [NL_PUBLIC_LINE.md](NL_PUBLIC_LINE.md) · [NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md)

---

## Session 1 — 2026-09-19 (~02:47Z)

| | |
|--|--|
| Result | **Pass** — join `success:true`, `step:Completed`, admit Allow |
| Player id | (session-1 fan; mock Steam64) |
| Platform user | `76561198000000001` (mock ownership) |
| Connect | `rimworld://play.20062006.xyz:25555` |
| Notes | First public admit on Path A. Demo overrides on (mock Steam/social, followers=0). Manifest may still have shown loopback HTTP until Day-1 env fix. |

---

## Session 2 — 2026-09-20 (~07:27–07:30Z)

| | |
|--|--|
| Result | **Pass** — join Completed after platform-user fix |
| Player id | `sp-fan-2` |
| Platform user | `76561198000000001` in **Platform user (Steam64)** field (not NL account id) |
| Streamer | `surrplexie-7c0056` — LIVE |
| Connect | `rimworld://play.20062006.xyz:25555` |
| Manifest HTTP | `https://play.20062006.xyz` (no `127.0.0.1:27020`) |
| Bridge | `wss://play.20062006.xyz/nl/v1?…` |
| Notes | First attempt failed `RequiresOwnership` / empty `platformUserId` (Steam64 was in NL account id; Platform user left as placeholder). Retried with Steam64 in Platform user → success. |

### Pre-session env fix (same night, ~02:00–02:20 local)

Set in `docker/vps-production-fleet.env` and recreated stack:

```text
NL_PUBLIC_BASE_URL=https://play.20062006.xyz
NL_PUBLIC_HTTP=https://play.20062006.xyz
NL_PUBLIC_WS=wss://play.20062006.xyz/nl/v1
NL_PUBLIC_HOST=play.20062006.xyz
NL_FORK_PUBLIC_CONNECT_HOST=play.20062006.xyz
```

Verified in container (`docker inspect` … `Config.Env`):

- `NL_PUBLIC_HTTP=https://play.20062006.xyz`
- `NL_PUBLIC_WS=wss://play.20062006.xyz/nl/v1`
- `NL_FORK_PUBLIC_CONNECT_HOST=play.20062006.xyz`

`GET /api/v1/t2-lite/status` → `ready:true`, `publicConnectHost:play.20062006.xyz`, `rimworldExample:rimworld://play.20062006.xyz:25555`.

---

## Session 3 — live Steam ownership — 2026-09-21 (~05:19Z)

| | |
|--|--|
| Result | **Pass** — join `success:true`, `step:Completed`, admit `Allow` |
| Player id | `sp-live-1` |
| Platform user | real Steam64 `76561199353783794` (not mock) |
| Streamer | `surrplexie-7c0056` — LIVE |
| Connect | `rimworld://play.20062006.xyz:25555` |
| Ownership | Live Steam Web API; profile `platformAppId` **`294100`** (RimWorld) |
| Notes | Earlier deny `Steam app hello-fork not in library` was stale dogfood `platformAppId`. Fixed via `POST /api/v1/dogfood/setup` with `{ gameId: "rimworld" }` then re-set streamer / join gate / Start. |

---

## Still open

- Native RimWorld/Together to `:25555` — optional
- Twitch/Discord OAuth + follower floor 50 — not wired (keep followers=0 until then)
- Optional: one deny with mock Steam64 `76561198000000001` to prove live gate

## Next

1. Optional native Together connect
2. Wire Twitch OAuth when ready; then raise `NL_FLEET_MIN_TWITCH_FOLLOWERS`
3. Keep freeze: no Cities / new titles until live admit is boring
