# Multi-game production deploy (Phase 8)

Extends the Phase 7 live production stack with all production fork images and the multi-game validation gate.

## Single-node VPS (Docker)

```bash
cp samples/fleet/multi-game-production.env.example docker/multi-game-production-fleet.env
# Set NL_OPERATOR_KEY, NL_BUS_TOKEN, STEAM_WEB_API_KEY, domains
# Set NL_LIVE_PRODUCTION_DEV=false, NL_GA_ALLOW_MOCK_IDENTITY=false

# Build fork images on host
docker build -f docker/fork-hello/Dockerfile -t nl-fork-hello:latest .
docker build -f docker/fork-minecraft/Dockerfile -t nl-fork-minecraft:latest .
docker build -f docker/fork-beamng/Dockerfile -t nl-fork-beamng:latest .

docker compose -f docker/docker-compose.multi-game-production.yml up -d --build
curl -fsS https://play.yourdomain.com/api/v1/multigame/status
```

Local validation (dev mode):

```powershell
powershell -File scripts/nl-multi-game-stack-up.ps1 -Validate
```

## Validation

```powershell
powershell -File scripts/nl-multi-game-validate.ps1 -OperatorKey "<from env file>"
```

Expected: **`MULTIGAME VALIDATION PASSED`**

## Ops UI

Open `https://play.yourdomain.com/multigame-ops.html` with the operator key.

See [docs/NL_MULTI_GAME_PRODUCTION.md](../../docs/NL_MULTI_GAME_PRODUCTION.md) and [docs/NL_MULTI_GAME_RUNBOOK.md](../../docs/NL_MULTI_GAME_RUNBOOK.md).
