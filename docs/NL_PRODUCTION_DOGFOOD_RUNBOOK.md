# NL Production Dogfood — Operator Runbook

## Prerequisites

- Windows 10/11 with Docker Desktop running
- .NET 8 SDK
- Ports **27020**, **443**, **8443** free
- Fork images built (stack-up does this automatically)

## Local gate (recommended)

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

powershell -File scripts/nl-production-dogfood-stack-down.ps1
powershell -File scripts/nl-production-dogfood-stack-up.ps1 -Validate
```

Pass line: **`PRODUCTION DOGFOOD VALIDATION PASSED`**

## Stack only (no validate)

```powershell
powershell -File scripts/nl-production-dogfood-stack-up.ps1 -SkipBuild
```

Save the operator key printed at startup (also in `docker/production-dogfood-fleet.env`).

## Validate against running stack

```powershell
$op = (Select-String -Path docker\production-dogfood-fleet.env -Pattern '^NL_OPERATOR_KEY=(.+)$').Matches.Groups[1].Value
powershell -File scripts/nl-production-dogfood-validate.ps1 -OperatorKey $op -AllGames
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Port 27020 busy | `powershell -File scripts/nl-production-dogfood-stack-down.ps1` |
| Docker provision fails | Docker Desktop running; images exist (`docker images nl-fork-hello`) |
| Join denied — ownership | Validate script runs dogfood setup (mock matrix); use Steam64 `76561198000000001` |
| Fork container exits | Check `docker logs nl-fork-*`; rebuild with `scripts/build-fork-images.ps1` |
| Minecraft port conflict | Stop other MC servers on 25565 |
| Orchestrator mode Mock | Rebuild stack — compose sets `NL_FORK_ORCHESTRATOR_MODE=docker` |
| GA signup 400 terms | Pass `termsAccepted: true` (validate script does this) |

## Ops console

http://127.0.0.1:27020/production-dogfood-ops.html

Paste operator key → check smoke boxes after manual runs → **Run production dogfood validation**.

## VPS production (next step)

1. Deploy Phase 14 stack on VPS with production-dogfood env overrides
2. Set real OAuth app redirect URIs to production domain
3. Set `NL_OWNERSHIP_MODE=live` and real Steam Web API key
4. Run validate script with `-BaseUrl https://your.domain`
