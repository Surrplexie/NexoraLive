# NexoraLive — public line

Get NL onto a real URL with **dev flags off** and **RimWorld** as the first public title.

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

1. Point DNS (all A records → VPS IPv4):

```
play.yourdomain.com
relay-us-east.yourdomain.com
relay-us-west.yourdomain.com
relay-eu-west.yourdomain.com
```

2. On the VPS:

```bash
sudo apt update && sudo apt install -y git curl
git clone https://github.com/Surrplexie/NexoraLive.git /opt/NexoraLive
cd /opt/NexoraLive
bash scripts/nl-vps-bootstrap.sh
```

3. From Windows, after DNS/TLS is up:

```powershell
powershell -File scripts/nl-vps-dns-check.ps1 -Domain play.yourdomain.com -ExpectedIp YOUR_VPS_IP
powershell -File scripts/nl-vps-validate.ps1 -BaseUrl https://play.yourdomain.com -OperatorKey YOUR_OPERATOR_KEY
```

Expected: **`VPS PRODUCTION VALIDATION PASSED`**

4. First live session: you as streamer, **RimWorld**, `/ga.html` → catalog `rimworld@1.0`.

**Full operator + streamer + fan walkthrough:** [NL_IDEAL_DEMO_GUIDE.md](NL_IDEAL_DEMO_GUIDE.md).

Full runbook: [NL_VPS_DEPLOY.md](NL_VPS_DEPLOY.md), [NL_VPS_DEPLOY_RUNBOOK.md](NL_VPS_DEPLOY_RUNBOOK.md), [NL_RIMWORLD.md](NL_RIMWORLD.md).

## Operator-only leftovers

These cannot be finished in git:

- [ ] Domain + DNS
- [ ] VPS with ports 80/443/3478 and **25555/tcp** (RimWorld T2-lite)
- [ ] `STEAM_WEB_API_KEY` in `docker/vps-production-fleet.env`
- [ ] Twitch/Discord OAuth redirect URIs (after base deploy)
- [ ] Push this repo to GitHub so the VPS clone is current
