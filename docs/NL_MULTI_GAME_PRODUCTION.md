# NL Multi-Game Production — Phase 8

Production fork images for **hello-fork**, **minecraft**, and **beamng** on the live production stack, with player join UX and partnership gates.

## Quick start (local dev validation)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-multi-game-stack-down.ps1
powershell -File scripts/nl-multi-game-stack-up.ps1 -Validate
```

Expected: **`MULTIGAME VALIDATION PASSED`**

The stack builds all three fork images, starts the live-production-style compose with `NL_MULTIGAME_PRODUCTION_ENABLED=true`, runs per-game dogfood smoke, and posts the validation gate.

## What Phase 8 adds

| Feature | Description |
|---------|-------------|
| **Multi-game program** | `NL_MULTIGAME_PRODUCTION_ENABLED` + required game list |
| **Catalog Docker images** | Each GA game must expose `dockerImage` in fork catalog |
| **Host image verification** | `docker image inspect` for nl-fork-hello/minecraft/beamng |
| **Per-game dogfood** | Fork provision + player join for each required game |
| **Partnership gate** | At-own-risk admit gate required for production |
| **Validation API** | `GET/POST /api/v1/multigame/validation` |
| **Ops UI** | `/multigame-ops.html` |

Compose: [`docker/docker-compose.multi-game-production.yml`](../docker/docker-compose.multi-game-production.yml)

## Operator pages

| URL | Purpose |
|-----|---------|
| `/multigame-ops.html` | Status, catalog images, validation gate |
| `/nl-client.html` | Player join flow |
| `/live-production-ops.html` | Live production checks (prerequisite) |
| `/ga.html` | Streamer registration |

## Environment

```env
NL_MULTIGAME_PRODUCTION_ENABLED=true
NL_MULTIGAME_REQUIRED_GAMES=hello-fork,minecraft,beamng
NL_LIVE_PRODUCTION_ENABLED=true
NL_PARTNERSHIP_ENABLED=true
NL_PARTNERSHIP_GATE_ADMIT=1
```

See [`samples/fleet/multi-game-production.env.example`](../samples/fleet/multi-game-production.env.example).

## Build fork images

```powershell
powershell -File scripts/build-fork-images.ps1 -Images all
# or: hello-fork, minecraft, beamng
```

Images:

| Game | Docker tag |
|------|------------|
| hello-fork | `nl-fork-hello:latest` |
| minecraft | `nl-fork-minecraft:latest` |
| beamng | `nl-fork-beamng:latest` |

## Validation checks

- Multi-game + live production + GA enabled
- Fork catalog enabled with Docker images per required game
- Host fork images verified (`hostImagesVerified` in POST body)
- Live production validation gate passed
- Partnership at-own-risk gate enabled
- Player join flow available (`/nl-client.html`)
- Per-game fork create/destroy + join smoke (validate script)

```powershell
powershell -File scripts/nl-multi-game-validate.ps1 -OperatorKey "<from multi-game-production-fleet.env>"
```

## Phase 8 exit criteria

- [x] Multi-game production compose stack
- [x] Build all production fork images script integration
- [x] Multi-game validation API + script
- [x] Ops UI + runbook
- [ ] VPS with real domain + all fork images pulled on host

Next: **Phase 9** — launch ops & trust (alerting, status page, legal, abuse hardening, backups).

See [NL_LAUNCH_OPS.md](NL_LAUNCH_OPS.md)
