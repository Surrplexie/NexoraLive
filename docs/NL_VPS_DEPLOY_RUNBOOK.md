# VPS deploy runbook (operator checklist)

## 1. Provision VPS

- [ ] Ubuntu 22.04 LTS
- [ ] 4 GB RAM minimum (8 GB for multigame dogfood)
- [ ] Public IPv4
- [ ] SSH key login enabled

## 2. DNS

- [ ] `play.` → VPS IP
- [ ] `relay-us-east.` → VPS IP
- [ ] `relay-us-west.` → VPS IP
- [ ] `relay-eu-west.` → VPS IP
- [ ] Propagation verified (`nl-vps-dns-check.ps1`)

## 3. Deploy

```bash
cd /opt/NexoraLive
bash scripts/nl-vps-bootstrap.sh
```

Save **operator key** offline.

## 4. Smoke test

```bash
curl -fsS https://play.yourdomain.com/health
curl -fsS https://play.yourdomain.com/api/v1/public-ga-launch/settings
```

## 5. Remote validation (from PC)

```powershell
powershell -File scripts/nl-vps-validate.ps1 -BaseUrl https://play.yourdomain.com -OperatorKey <key>
```

## 6. Go live

- [ ] Streamer signup at `/ga.html`
- [ ] Identity link at `/identity-link.html`
- [ ] Status page public at `/status.html`
- [ ] Daily backup cron: `POST /api/v1/launch-ops/backup/run`
- [ ] Alert webhook in fleet env

## 7. Signoff

- [ ] `NL_PUBLIC_GA_LAUNCH_DEV=false` confirmed on VPS
- [ ] Operator signoff in `/public-ga-launch-ops.html`
- [ ] Production dogfood passed against public URL
