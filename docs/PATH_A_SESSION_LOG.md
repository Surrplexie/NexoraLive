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

- ~~Native RimWorld/Together to `:25555`~~ → **done** (session 4)
- Twitch/Discord OAuth + follower floor 50 — not wired (keep followers=0 until then)
- Optional: one deny with mock Steam64 `76561198000000001` to prove live gate
- NL Harmony bridge inside Together world (in-game Allow/Block) — not wired yet
- Native NL desktop door (T3) — not started

## Public ready — **PASSED** 2026-09-22

`scripts/nl-public-ready-finish.ps1` → **`PUBLIC READY (VPS)`**

| Gate | Result |
|------|--------|
| Fleet / production_ready | PASS |
| Legal compliance (Phase 13) | PASS |
| Public GA launch (Phase 14) | PASS |

Host: `https://play.20062006.xyz` · GA `devMode=false` · support `support@20062006.xyz`

## Session 4 — native Together world — 2026-09-25 (~05:35–05:37Z)

| | |
|--|--|
| Result | **Pass** — RimWorld Together Direct Connect → in-colony (~8 ms) |
| Server | `/opt/rimworld-together/RTServer` **26.8.31.1** on VPS `0.0.0.0:25555` |
| Client | Workshop Together `3005289691` · Direct Connect `play.20062006.xyz` / `25555` |
| Player | `ByteSizedKai` from `207.110.231.254` · first-join admin · world + settlement created |
| NL sidecar | Stopped for this test (`nl-fork-*` freed port); session-host may still show Running |
| Notes | Sidecar banner alone cannot Together-handshake. Official zip binary is **`RTServer`** (not `GameServer`). Docker image `ghcr.io/mrgreaterthan/rimworld-together` was **25.3.9.1** (version mismatch → silent menu bounce); switching to zip **26.8.31.1** fixed join. See [NL_RIMWORLD_TOGETHER_VPS.md](NL_RIMWORLD_TOGETHER_VPS.md). |

## Next

1. Set a Together **server password** (discovery is ENABLED / public browser)
2. Optional: wire NL Harmony bridge into this Together world for live `.nle` cancel
3. Native NL door (Win app / CLI against `play.20062006.xyz`) — ideal-plan piece 1
4. Twitch OAuth → followers=50 later
5. Keep freeze: no Cities / new titles
6. Do **not** Operator-Start rimworld fork while `RTServer` owns `:25555`