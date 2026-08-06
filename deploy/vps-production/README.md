# VPS production deploy (Phase 15)

Single-node production stack with **Let's Encrypt**, **Docker forks**, and **all dev flags off**.

## Quick start

```bash
git clone https://github.com/Surrplexie/NexoraLive.git /opt/NexoraLive
cd /opt/NexoraLive
bash scripts/nl-vps-bootstrap.sh
```

## Docs

- [docs/NL_VPS_DEPLOY.md](../../docs/NL_VPS_DEPLOY.md)
- [docs/NL_VPS_DEPLOY_RUNBOOK.md](../../docs/NL_VPS_DEPLOY_RUNBOOK.md)

## Windows remote deploy

```powershell
powershell -File scripts/nl-vps-deploy-from-windows.ps1 -VpsHost YOUR_VPS_IP
```
