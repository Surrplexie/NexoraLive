# Multi-Game Production Runbook — Phase 8

Operator checklist for running hello-fork, minecraft, and beamng on the public production stack.

## Prerequisites

- Phase 7 live production stack validated (`LIVE PRODUCTION VALIDATION PASSED`)
- Docker on host with fork images built or pulled
- Partnership program enabled for at-own-risk games

## Local validation

```powershell
powershell -File scripts/nl-multi-game-stack-down.ps1
powershell -File scripts/nl-multi-game-stack-up.ps1 -Validate
```

## VPS deploy

1. Copy `samples/fleet/multi-game-production.env.example` → `docker/multi-game-production-fleet.env`
2. Set operator key, bus token, Steam Web API key, public URL
3. Set `NL_LIVE_PRODUCTION_DEV=false`, `NL_GA_ALLOW_MOCK_IDENTITY=false`
4. Build or pull fork images on the host:

```bash
docker build -f docker/fork-hello/Dockerfile -t nl-fork-hello:latest .
docker build -f docker/fork-minecraft/Dockerfile -t nl-fork-minecraft:latest .
docker build -f docker/fork-beamng/Dockerfile -t nl-fork-beamng:latest .
```

5. Start stack:

```bash
docker compose -f docker/docker-compose.multi-game-production.yml up -d --build
```

6. Run validation from operator workstation (must reach API + Docker on host):

```powershell
powershell -File scripts/nl-multi-game-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -OperatorKey "<NL_OPERATOR_KEY>"
```

## Per-game smoke (manual)

```powershell
powershell -File scripts/nl-dogfood-flow.ps1 -GameId hello-fork -ExpectProvisioner docker -SkipImageBuild
powershell -File scripts/nl-dogfood-flow.ps1 -GameId minecraft -ExpectProvisioner docker -SkipImageBuild
powershell -File scripts/nl-dogfood-flow.ps1 -GameId beamng -ExpectProvisioner docker -SkipImageBuild
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `host_fork_images` FAIL | Build images on Docker host; re-run validate with verified flag |
| `live_production_gate` FAIL | Fix Phase 7 checks first via `nl-live-production-validate.ps1` |
| `partnership_gate` FAIL | Set `NL_PARTNERSHIP_ENABLED=true` and `NL_PARTNERSHIP_GATE_ADMIT=1` |
| Dogfood timeout on minecraft | Increase teardown grace; check fork container logs |
| Port 27020 busy | Run `nl-multi-game-stack-down.ps1` and stop other NL stacks |

## Rollback

```powershell
powershell -File scripts/nl-multi-game-stack-down.ps1
# Fall back to live-production-only stack if needed
powershell -File scripts/nl-live-production-stack-up.ps1
```
