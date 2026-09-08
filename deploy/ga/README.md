# General availability deploy

Extends the production fleet stack with open signup, multi-game catalog, SLA, and compliance.

```bash
cp samples/fleet/ga.env.example docker/ga-fleet.env
# Set NL_OPERATOR_KEY, NL_BUS_TOKEN, STEAM_WEB_API_KEY, domains
# Set NL_FORK_DOCKER_WORKSPACE_HOST_ROOT to host path of docker/ga-data
# Ensure NL_BETA_ENABLED=false and NL_GA_ENABLED=true

docker compose -f docker/docker-compose.ga-fleet.yml up -d --build
curl -fsS https://play.yourdomain.com/api/v1/ga/status
```

Local validation:

```powershell
powershell -File scripts/nl-ga-stack-up.ps1 -Validate
```

Full guide: [docs/NL_GENERAL_AVAILABILITY.md](../../docs/NL_GENERAL_AVAILABILITY.md) · Runbook: [docs/NL_GA_RUNBOOK.md](../../docs/NL_GA_RUNBOOK.md)
