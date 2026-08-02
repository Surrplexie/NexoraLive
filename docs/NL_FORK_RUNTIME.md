# NL Fork Runtime — Phase P

Server-side enforcement inside NL-hosted fork images. The hello-fork reference runtime
implements `IForkRuntime` with propose-then-commit: events go to NL, **Block** decisions
prevent in-game effects without RCON/UDP.

## Architecture

```text
  HelloForkRuntime (in fork container)
        │ TryShoot / TryJoin / …
        ▼
  ForkStateValidator (local packet/state checks)
        ▼
  ForkNlBridgeClient ──ws──► NL SessionHost RuleEngine
        ◄── action NDJSON ──
        ▼
  ForkActionApplicator (kick, strip weapon, recover, …)
```

## Quick start (local)

```powershell
# Terminal 1 — session server with fork rules
$env:NL_DEMO_MODE = "0"
dotnet run --project src/NL.SessionHost.Web
# Start session from operator UI or POST /api/v1/session/start with fork-hello.nle

# Terminal 2 — hello-fork runtime
dotnet run --project src/NL.Fork.Runtime -- `
  --url "ws://127.0.0.1:27021/nl/v1?token=YOUR_BUS_TOKEN" `
  --mods samples/fork/hello-fork.mods.json `
  --loop --interval 8
```

Or run the smoke script:

```powershell
powershell -File scripts/nl-fork-smoke.ps1
```

## Embedded mode (no bus)

```powershell
dotnet run --project src/NL.Fork.Runtime -- `
  --config samples/configs/fork-hello.nle `
  --loop --interval 5
```

## Operator API

`GET /api/v1/fork/status` — reads `fork-status.json` written by the runtime (public read).

## Docker

```bash
docker build -f docker/fork-hello/Dockerfile -t nl-fork-hello .
docker run --rm -e NL_FORK_WS_URL=ws://host.docker.internal:27021/nl/v1?token=... nl-fork-hello --loop
```

## Exit criteria (Phase P)

On NL-hosted fork with `event shoot: block`, `TryShootAsync` returns `Committed=false` and
target health unchanged — verified in `tests/NL.Fork.Core.Tests`.

## Migrating real titles

| Title | Path |
|-------|------|
| **Hello-fork** | Reference `IForkRuntime` in `NL.Fork.Core` |
| **Minecraft dedicated** | Implement `IForkRuntime` in Paper/Purpur plugin; same WS contract |
| **BeamNG** | Replace file-tail bridge with in-process fork hook + UDP actions optional |

See [NL_INTEGRATION_SPEC.md](NL_INTEGRATION_SPEC.md) and [NL_FORK_PLATFORM.md](NL_FORK_PLATFORM.md).
