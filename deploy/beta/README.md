# Public beta deploy

Extends the production fleet stack with waitlist + live Steam identity.

```bash
cp samples/fleet/beta.env.example docker/beta-fleet.env
# Set NL_OPERATOR_KEY, NL_BUS_TOKEN, STEAM_WEB_API_KEY, domains
# Set NL_FORK_DOCKER_WORKSPACE_HOST_ROOT to host path of docker/beta-data

docker compose -f docker/docker-compose.beta-fleet.yml up -d --build
curl -fsS https://beta.yourdomain.com/api/v1/beta/status
```

Local validation:

```powershell
powershell -File scripts/nl-beta-stack-up.ps1 -Validate
```

Full guide: [docs/NL_PUBLIC_BETA.md](../../docs/NL_PUBLIC_BETA.md) · Runbook: [docs/NL_BETA_RUNBOOK.md](../../docs/NL_BETA_RUNBOOK.md)
