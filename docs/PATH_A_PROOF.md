# Path A proof — public NL (2026-09-18 / 19)

Operator evidence that NexoraLive is live on a real URL with RimWorld T2-lite and a completed fan join. Staging hostname: **`20062006.xyz`**. Not GA marketing.

## Stack

| Item | Value |
|------|--------|
| Domain | `20062006.xyz` (Cloudflare Registrar) |
| Play host | `https://play.20062006.xyz` |
| VPS | OVHcloud US VPS-2 · Virginia · Ubuntu 24.04 · 8 GB · `40.160.88.114` |
| Repo on box | `/opt/NexoraLive` (clone of `Surrplexie/NexoraLive`) |
| Compose | `docker/docker-compose.vps-production.yml` + `docker/.env.vps` |

## Proven checks

| Check | Result | When |
|-------|--------|------|
| `GET /health` | `status:ok`, `publicMode:true`, `hardening:true`, `demoMode:false` | 2026-09-18 |
| Public demo page | Session Running; rule Allow/Block from Try-a-rule | 2026-09-18 |
| `GET /api/v1/t2-lite/status` | `ready:true`, `rimworldExample: rimworld://play.20062006.xyz:25555` | 2026-09-18 |
| Operator session | Streamer `surrplexie-7c0056`, Game id `rimworld`, config `rimworld.nle`, fork orchestrator + join gate on | 2026-09-18 |
| Fan join flow (`/nl-client.html`) | `success:true`, `step:Completed`, admit `Allow`, clipboard `rimworld://play.20062006.xyz:25555` | 2026-09-19 (~02:47Z) |
| Mock Steam64 for admit | `76561198000000001` with `NL_OWNERSHIP_MODE=mock` (demo only) | 2026-09-18 |

## Demo overrides still on the box (revert before real GA)

Temporary for first two-person pass ([NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md) §1.4):

- `NL_FLEET_MIN_TWITCH_FOLLOWERS=0`
- `NL_OWNERSHIP_MODE=mock` / `NL_SOCIAL_MODE=mock`
- Compose: `NL_GA_REQUIRE_LIVE_IDENTITY=false`, `NL_GA_ALLOW_MOCK_IDENTITY=true`

Restore live Steam/social + follower floor **50** when the admit loop is boring.

## Known gaps (not blockers for “session 1 NL admit”)

- Manifest still may show `http://127.0.0.1:27020` unless `NL_PUBLIC_BASE_URL=https://play.20062006.xyz` is set and stack recreated
- Native RimWorld/Together connect to `:25555` optional — not required to count NL admit
- **Session 2** not yet run (Days 31–60 kill metric)
- Twitch/Discord OAuth redirect URIs not wired
- Apex Worker marketing page separate from `play.`

## Docker down / up (on the VPS)

From `/opt/NexoraLive`:

```bash
# Stop stack + remove leftover nl-fork-* containers
sudo bash scripts/nl-vps-stack-down.sh

# Start again (uses saved env; does not re-prompt bootstrap)
sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps up -d

# Or full rebuild + start
sudo bash scripts/nl-vps-deploy.sh
```

Quick status:

```bash
sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps ps
curl -fsS https://play.20062006.xyz/health
curl -fsS https://play.20062006.xyz/api/v1/t2-lite/status
```

## Next metric

1. Fix `NL_PUBLIC_BASE_URL` if still loopback in join manifest  
2. Optional: native connect to `rimworld://play.20062006.xyz:25555`  
3. **Second** live session (friend or second browser) — required before Cities / new titles  
4. Revert mock → live ownership when ready  

See also: [NL_PUBLIC_LINE.md](NL_PUBLIC_LINE.md) · [NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md) · [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md)
