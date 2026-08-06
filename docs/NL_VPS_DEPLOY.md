# NL VPS Production Deploy

Full public deploy: **domain + Let's Encrypt + Docker forks + GA launch stack** with all dev flags off.

## What you need

| Item | Example |
|------|---------|
| VPS | Ubuntu 22.04+, 4 GB RAM, 2 vCPU, 40 GB disk |
| Domain | `yourdomain.com` |
| DNS access | A records → VPS public IP |
| Ports open | **80**, **443**, **3478** (tcp+udp) |
| Steam Web API key | [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey) (live ownership) |

## DNS records (all → VPS IP)

```
play.yourdomain.com          A
relay-us-east.yourdomain.com A
relay-us-west.yourdomain.com A
relay-eu-west.yourdomain.com A
turn.yourdomain.com          A   (optional label for TURN docs)
```

Wait for propagation (5–30 min). Check:

```powershell
powershell -File scripts/nl-vps-dns-check.ps1 -Domain play.yourdomain.com -ExpectedIp YOUR_VPS_IP
```

---

## Path A — Deploy on the VPS (recommended)

SSH into the VPS:

```bash
sudo apt update && sudo apt install -y git curl
git clone https://github.com/Surrplexie/NexoraLive.git /opt/NexoraLive
cd /opt/NexoraLive
bash scripts/nl-vps-bootstrap.sh
```

`bootstrap` will:
1. Install Docker (if missing)
2. Prompt for domain + generate secrets → `docker/.env.vps` + `docker/vps-production-fleet.env`
3. Build fork images
4. Start stack with Caddy TLS
5. Print operator key

Verify:

```bash
curl -fsS https://play.yourdomain.com/health
```

---

## Path B — Deploy from your Windows PC over SSH

On VPS once (clone repo):

```bash
git clone https://github.com/Surrplexie/NexoraLive.git /opt/NexoraLive
cd /opt/NexoraLive && bash scripts/nl-vps-init-env.sh
```

Copy env back to PC (save operator key), or note key from VPS output.

From Windows:

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive
powershell -File scripts/nl-vps-deploy-from-windows.ps1 -VpsHost YOUR_VPS_IP -VpsUser root
```

With SSH key:

```powershell
powershell -File scripts/nl-vps-deploy-from-windows.ps1 -VpsHost YOUR_VPS_IP -VpsUser ubuntu -SshKey C:\Users\you\.ssh\id_ed25519
```

---

## Validate from Windows

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive
powershell -File scripts/nl-vps-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey YOUR_OPERATOR_KEY
```

Expected: **`VPS PRODUCTION VALIDATION PASSED`**

Full dogfood on VPS (requires Steam key + fork images on server):

```powershell
powershell -File scripts/nl-production-dogfood-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey YOUR_OPERATOR_KEY `
  -AllGames `
  -SkipClientBuild
```

---

## Upgrade / redeploy

On VPS:

```bash
cd /opt/NexoraLive
git pull --ff-only
bash scripts/nl-vps-deploy.sh
```

Stop:

```bash
bash scripts/nl-vps-stack-down.sh
```

---

## OAuth (after base deploy)

Set in `docker/vps-production-fleet.env` on VPS, then redeploy:

| Service | Redirect URI |
|---------|----------------|
| Twitch | `https://play.yourdomain.com/api/v1/social/oauth/twitch/callback` |
| Discord | `https://play.yourdomain.com/api/v1/social/oauth/discord/callback` |
| Steam OpenID | `https://play.yourdomain.com/api/v1/identity/oauth/steam/callback` |

Templates: `samples/social/twitch-oauth.env.example`, `samples/identity/platform-oauth.env.example`

---

## Files

| File | Purpose |
|------|---------|
| `docker/docker-compose.vps-production.yml` | Production compose |
| `docker/.env.vps` | Caddy domain + ACME email |
| `docker/vps-production-fleet.env` | Secrets + NL env (gitignored) |
| `docker/edge-vps/Caddyfile` | TLS + relay subdomains |
| `samples/fleet/vps-production.env.example` | Env template |

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Caddy no certificate | DNS must point to VPS; ports 80/443 open; wait for propagation |
| `connection refused` on health | `docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps logs session-host` |
| Fork provision fails | `docker images \| grep nl-fork`; re-run `bash scripts/build-fork-images.sh all` |
| Join denied ownership | Set `STEAM_WEB_API_KEY` or use mock only on local dogfood stack |
| 401 on ops APIs | Use `NL_OPERATOR_KEY` from `vps-production-fleet.env` |

See also: [`NL_PRODUCTION_CUTOVER_RUNBOOK.md`](NL_PRODUCTION_CUTOVER_RUNBOOK.md), [`NL_VPS_DEPLOY_RUNBOOK.md`](NL_VPS_DEPLOY_RUNBOOK.md)
