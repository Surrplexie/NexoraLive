# NexoraLive — public line

Get NL onto a real URL with **dev flags off** and **RimWorld** as the first public title.

**Path A live (2026-09-18/20):** `https://play.20062006.xyz` — health OK, T2-lite `ready:true` (`rimworld://play.20062006.xyz:25555`), sessions **1 + 2** Completed. Proof: [PATH_A_PROOF.md](PATH_A_PROOF.md) · [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md). Demo still uses **mock** ownership / 0 followers — revert before GA. **Next:** live ownership. Do not start Cities / Away.

This is the week-2 gate: a stranger can open `/play.html`, a streamer can sign up, and a RimWorld session can start on NL-hosted infrastructure.

## What “public line” means

| In | Out |
| --- | --- |
| `NL_PUBLIC_GA_LAUNCH_DEV=false` | Kenshi / Cities adapters |
| HTTPS on `play.yourdomain.com` | Mainnet money, civilization lore |
| Live Steam ownership (when a key is set) | Fake rival companies |
| RimWorld + Minecraft + hello-fork smokes | BeamNG as a launch blocker |
| Operator signoff recorded | |

## Code path (done in-repo)

- VPS compose publishes session-host on **loopback** `27020`/`27021` so Docker forks can call back via `host.docker.internal`.
- **T2-lite:** RimWorld game port **25555/tcp** on the VPS host; manifests use `NL_FORK_PUBLIC_CONNECT_HOST` ([NL_T2_LITE.md](NL_T2_LITE.md)).
- `/data` is a **host bind mount** (`NL_FORK_DOCKER_WORKSPACE_HOST_ROOT`), not a named volume — fork `-v` paths match files the session-host wrote.
- `docker/`, `integrations/`, and `deploy/` are in the git allowlist so `git clone` on a VPS actually gets the stack.
- Required GA games: `hello-fork,minecraft,rimworld`.
- Default fork image: `nl-fork-rimworld:latest`.

## Local rehearsal (this machine)

Docker Desktop running. Stop other NL stacks first.

```powershell
cd C:\Users\surrp\Downloads\!a\future\products\nl
powershell -File scripts/nl-public-line-ready.ps1
```

Expected: **`PUBLIC LINE READY (local)`** then a remaining-operator list.

Skip Docker image builds if they already exist:

```powershell
powershell -File scripts/nl-public-line-ready.ps1 -SkipForkImages -SkipBuild
```

## Real public (needs your VPS + domain)

You still supply: Ubuntu VPS (4 GB+), DNS, Let's Encrypt email, Steam Web API key.

1. Cloudflare DNS for `20062006.xyz` — **A records → `40.160.88.114`**, proxy **off** (grey cloud) on every one of these:

```
play.20062006.xyz
relay-us-east.20062006.xyz
relay-us-west.20062006.xyz
relay-eu-west.20062006.xyz
```

Apex (`20062006.xyz`) can stay on the Worker. Do **not** orange-cloud `play.` (breaks 25555 / Let's Encrypt on the VPS).

2. OVH firewall / security group: allow **80/tcp, 443/tcp, 3478/tcp+udp, 25555/tcp**, and **22/tcp** for SSH.

3. On the VPS (SSH as root or ubuntu; use the password/key OVH emailed):

```bash
sudo apt update && sudo apt install -y git curl
git clone https://github.com/Surrplexie/NexoraLive.git /opt/NexoraLive
cd /opt/NexoraLive
bash scripts/nl-vps-bootstrap.sh
```

When prompted, domain = `play.20062006.xyz`. Save the printed **operator key**.

If GitHub is stale vs your PC: from Windows use `scripts/nl-vps-deploy-from-windows.ps1` against `C:\Users\surrp\Documents\GitHub\NexoraLive` instead (see [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md) Path B).

4. From Windows, after DNS/TLS is up:

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive
powershell -File scripts/nl-vps-dns-check.ps1 -Domain play.20062006.xyz -ExpectedIp 40.160.88.114
powershell -File scripts/nl-vps-validate.ps1 -BaseUrl https://play.20062006.xyz -OperatorKey YOUR_OPERATOR_KEY
```

Expected: **`VPS PRODUCTION VALIDATION PASSED`**

5. First live session: you as streamer, **RimWorld**, `/ga.html` → catalog `rimworld@1.0`.

**Full operator + streamer + fan walkthrough:** [NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md).

Full runbook: [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md), [NL_VPS_DEPLOY_RUNBOOK.md](NL_VPS_DEPLOY_RUNBOOK.md), [NL_RIMWORLD.md](NL_RIMWORLD.md).

## Operator-only leftovers

These cannot be finished in git:

- [x] Domain — `20062006.xyz` (Cloudflare)
- [x] VPS — OVH US VPS-2 `40.160.88.114` (Virginia, Ubuntu 24.04, 8 GB)
- [x] DNS A records for `play.` + relays → VPS IP (**grey cloud**)
- [x] OVH + ufw ports 80/443/3478/25555
- [x] Bootstrap / deploy → `https://play.20062006.xyz`
- [x] `STEAM_WEB_API_KEY` present (rotate if pasted in chat)
- [ ] Twitch/Discord OAuth redirect URIs (after base deploy)
- [x] GitHub sync for CredentialStore build fix (`9562b28`)
- [x] RimWorld session 1 + fan join **Completed** (mock Steam64) — [PATH_A_PROOF.md](PATH_A_PROOF.md)
- [x] Public HTTP/WS + `NL_FORK_PUBLIC_CONNECT_HOST` (no loopback in manifest) — 2026-09-20
- [x] Session 2 (`sp-fan-2`) — [PATH_A_SESSION_LOG.md](PATH_A_SESSION_LOG.md)
- [ ] Restore live ownership / followers=50 when demo is boring

### Docker down / up (VPS, `/opt/NexoraLive`)

```bash
sudo bash scripts/nl-vps-stack-down.sh
sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps up -d
# full rebuild: sudo bash scripts/nl-vps-deploy.sh
```

Then: attempt a **second** session. If the URL exists and session 2 never happens, hobby or change wedge — do not add a title.
