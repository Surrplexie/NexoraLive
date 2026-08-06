# NL Production Dogfood

End-to-end production-style validation: **GA stack + real Docker fork images + streamer signup + identity account + player join + teardown**.

This is the ROADMAP **next track** after Phase 14 public GA launch.

## Quick start (one command)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-production-dogfood-stack-down.ps1
powershell -File scripts/nl-production-dogfood-stack-up.ps1 -Validate
```

Expected: **`PRODUCTION DOGFOOD VALIDATION PASSED`**

Requires **Docker Desktop** running (session-host container mounts `docker.sock` and spawns fork containers on the host).

## What this track adds

| Feature | Description |
|---------|-------------|
| **Production dogfood program** | `NL_PRODUCTION_DOGFOOD_ENABLED` master switch |
| **Docker fork provisioner** | `NL_FORK_ORCHESTRATOR_MODE=docker` (not mock) |
| **Full onboarding path** | GA signup → identity account → Steam link → NL Client join |
| **Per-game smokes** | hello-fork (required), minecraft + beamng (with `-AllGames`) |
| **Validation gate** | `GET/POST /api/v1/production-dogfood/validation` |
| **Ops UI** | `/production-dogfood-ops.html` |
| **Last run record** | Stored at `{NL_DATA_ROOT}/fleet/production-dogfood-last-run.json` |

Compose: [`docker/docker-compose.production-dogfood.yml`](../docker/docker-compose.production-dogfood.yml)

## Validation flow (automated)

The validate script runs:

1. Health + Docker orchestrator mode check
2. Public pages (`/play.html`, `/ga.html`, `/identity-link.html`, …)
3. Streamer registration at `/api/v1/ga/streamers/register` (terms accepted)
4. NL identity account create + manual Steam link (mock ownership matrix)
5. `nl-dogfood-flow.ps1` per game — setup → start → docker fork → client join → teardown
6. Production dogfood validation gate POST

### hello-fork only

```powershell
powershell -File scripts/nl-production-dogfood-validate.ps1
```

### All catalog games (hello-fork + minecraft + beamng)

```powershell
powershell -File scripts/nl-production-dogfood-validate.ps1 -AllGames
```

## Environment

| Variable | Default (compose) | Description |
|----------|-------------------|-------------|
| `NL_PRODUCTION_DOGFOOD_ENABLED` | `true` | Enable program + APIs |
| `NL_PRODUCTION_DOGFOOD_DEV` | `true` | Relax gate requirements locally |
| `NL_PRODUCTION_DOGFOOD_REQUIRED_GAMES` | `hello-fork,minecraft,beamng` | Games for multigame gate |
| `NL_PRODUCTION_DOGFOOD_REQUIRE_MULTIGAME` | `true` | Require minecraft + beamng smokes |
| `NL_FORK_ORCHESTRATOR_MODE` | `docker` | Real fork containers |
| `NL_OWNERSHIP_MODE` | `mock` | Local mock ownership matrix |
| `NL_SOCIAL_MODE` | `mock` | Social gate fixtures (join gate off in dogfood profile) |

Operator key is written to `docker/production-dogfood-fleet.env` on stack-up.

## REST API

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/production-dogfood/settings` | Public program info |
| GET | `/api/v1/production-dogfood/status` | Orchestrator mode + last run |
| GET | `/api/v1/production-dogfood/validation` | Dry-run gate (all flags false) |
| POST | `/api/v1/production-dogfood/validation/run` | Run gate with smoke flags |

## Manual browser replay

After stack-up:

1. **Streamer signup** — http://127.0.0.1:27020/ga.html
2. **Identity link** — http://127.0.0.1:27020/identity-link.html (create account, link Steam)
3. **Operator** — http://127.0.0.1:27020/operator.html → Load dogfood profile → Start session
4. **NL Client** — http://127.0.0.1:27020/nl-client.html → Run join flow
5. **Teardown** — Stop session; verify fork destroyed in fork orchestrator UI

See also [`docs/NL_DOGFOOD_FLOW.md`](NL_DOGFOOD_FLOW.md) for step-by-step operator/client details.

## Tests

```powershell
dotnet test tests/NL.Fleet.Tests/NL.Fleet.Tests.csproj --filter ProductionDogfood
```

## Next

- **VPS production deploy** — run the same validate script against a real domain with live OAuth credentials
- Enable `NL_OWNERSHIP_MODE=live` + real `STEAM_WEB_API_KEY` for live ownership dogfood
- Wire Twitch/Discord OAuth for social-gated dogfood sessions

See [`docs/NL_PRODUCTION_DOGFOOD_RUNBOOK.md`](NL_PRODUCTION_DOGFOOD_RUNBOOK.md).
