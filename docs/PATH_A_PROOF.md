# Path A proof — public NL (2026-09-18 → 20)

Operator evidence that NexoraLive is live on a real URL with RimWorld T2-lite and **two** completed fan joins. Staging hostname: **`20062006.xyz`**. Not GA marketing.

Session-by-session detail: [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md).

## Stack

| Item | Value |
|------|--------|
| Domain | `20062006.xyz` (Cloudflare Registrar) |
| Play host | `https://play.20062006.xyz` |
| VPS | OVHcloud US VPS-2 · Virginia · Ubuntu 24.04 · 8 GB · `40.160.88.114` |
| Repo on box | `/opt/NexoraLive` (clone of `Surrplexie/NexoraLive`) |
| Compose | `docker/docker-compose.vps-production.yml` + `docker/.env.vps` |
| Fleet env | `docker/vps-production-fleet.env` |

## Proven checks

| Check | Result | When |
|-------|--------|------|
| `GET /health` | `status:ok`, `publicMode:true`, `hardening:true`, `demoMode:false` | 2026-09-18 |
| Public demo page | Session Running; rule Allow/Block from Try-a-rule | 2026-09-18 |
| `GET /api/v1/t2-lite/status` | `ready:true`, `rimworldExample: rimworld://play.20062006.xyz:25555` | 2026-09-18; reconfirmed 2026-09-20 |
| Operator session | Streamer `surrplexie-7c0056`, Game id `rimworld`, config `rimworld.nle`, fork orchestrator + join gate on | 2026-09-18+ |
| **Session 1** fan join | `success:true`, `step:Completed`, admit Allow, clipboard `rimworld://play.20062006.xyz:25555` | 2026-09-19 (~02:47Z) |
| Public URL env | `NL_PUBLIC_HTTP` / `NL_PUBLIC_WS` / `NL_FORK_PUBLIC_CONNECT_HOST` → `play.20062006.xyz` in container; manifest `httpBaseUrl` HTTPS public | 2026-09-20 |
| **Session 2** fan join | Player `sp-fan-2`, mock Steam64 `76561198000000001`, Completed; public fork URI | 2026-09-20 (~07:30Z) |

## Demo overrides still on the box (revert before real GA)

Temporary for first two-person pass ([NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md) §1.4):

- `NL_FLEET_MIN_TWITCH_FOLLOWERS=0`
- `NL_OWNERSHIP_MODE=mock` / `NL_SOCIAL_MODE=mock`
- Compose may still have `NL_GA_REQUIRE_LIVE_IDENTITY=false`, `NL_GA_ALLOW_MOCK_IDENTITY=true`

Restore live Steam/social + follower floor **50** when the admit loop is boring.

## Known gaps

- Native RimWorld/Together connect to `:25555` optional — not required to count NL admit
- Twitch/Discord OAuth redirect URIs not wired
- Apex Worker marketing page separate from `play.`
- Live ownership cutover not done

## Docker down / up (on the VPS)

From `/opt/NexoraLive`:

```bash
sudo bash scripts/nl-vps-stack-down.sh
sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps up -d
# Or full rebuild: sudo bash scripts/nl-vps-deploy.sh
```

Quick status:

```bash
sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps ps
curl -fsS https://play.20062006.xyz/health
curl -fsS https://play.20062006.xyz/api/v1/t2-lite/status
```

## Next metric

1. ~~Public HTTP / fork host (no loopback in manifest)~~ ✅ 2026-09-20  
2. ~~**Session 2**~~ ✅ 2026-09-20 — see [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md)  
3. Optional: native connect to `rimworld://play.20062006.xyz:25555`  
4. Revert mock → live ownership when ready  

See also: [NL_PUBLIC_LINE.md](NL_PUBLIC_LINE.md) · [NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md) · [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md)
