# NL Headless Linux & Docker (Phase C)

Cross-platform **operator tooling** and **headless NL.Server** for dedicated servers, cloud
hosts, and CI — without Windows WinForms.

## What Phase C adds

| Component | Port | Role |
|-----------|------|------|
| `NL.Server` CLI | — | Headless rule engine (Linux/macOS/Windows) |
| `NL.SessionHost.Web` | 27020 / 27021 | Session dashboard + bridge WebSocket |
| `NL.Moderation.Web` | 27030 | Moderation dashboard (warn/ban/graylist/clear) |
| `NL_DATA_ROOT` | — | Shared data directory override for containers |

Windows WinForms apps (`NL.SessionHost`, `NL.ModerationConsole`, `NL.ConfigEditor`) remain
available; Mac/Linux operators use the web consoles instead.

## Quick start (native Linux)

```bash
# Publish linux-x64 artifacts
bash scripts/publish-linux.sh

# Session Host web
export NL_DATA_ROOT=~/.local/share/NL
dotnet artifacts/publish-linux/SessionHostWeb/NL.SessionHost.Web.dll
# → http://127.0.0.1:27020

# Moderation web (separate terminal)
dotnet artifacts/publish-linux/ModerationWeb/NL.Moderation.Web.dll
# → http://127.0.0.1:27030
```

Or run from source:

```bash
dotnet run --project src/NL.SessionHost.Web
dotnet run --project src/NL.Moderation.Web
```

Headless NL.Server only (no dashboard):

```bash
bash scripts/run-headless-linux.sh
# or with custom source:
NL_SOURCE=ws://0.0.0.0:27021/nl/v1 bash scripts/run-headless-linux.sh
```

## Environment variables

| Variable | Default | Used by |
|----------|---------|---------|
| `NL_DATA_ROOT` | `%LOCALAPPDATA%/NL` or `~/.local/share/NL` | All apps — profiles, moderation log, session profile |
| `NL_BIND` | `127.0.0.1` | Web hosts (`0.0.0.0` in Docker) |
| `NL_HTTP_PORT` | `27020` | Session Host web |
| `NL_WS_PORT` | `27021` | Session bus WebSocket |
| `NL_BUS_TOKEN` | random | Bridge auth token |
| `NL_MOD_HTTP_PORT` | `27030` | Moderation web |
| `NL_MODERATION_LOG` | `$NL_DATA_ROOT/moderation.jsonl` | Moderation web override |
| `NL_SP_STORE` | `$NL_DATA_ROOT/sp-profiles.json` | Moderation web override |

## Docker (operator stack)

From the repo root:

```bash
docker compose -f docker/docker-compose.yml up --build
```

**Public demo with TLS (Phase F):** see [NL_DEPLOY.md](NL_DEPLOY.md) — `docker/docker-compose.demo.yml` + Caddy.

| URL | Service |
|-----|---------|
| http://localhost:27020 | Session Host dashboard + REST |
| ws://localhost:27021/nl/v1?token=… | Game bridge (token printed at startup) |
| http://localhost:27030 | Moderation console |

Both containers mount the same `nl-data` volume at `/data` (`NL_DATA_ROOT=/data`).

Build individual images:

```bash
docker build -f docker/Dockerfile --target session-host -t nl-session-host .
docker build -f docker/Dockerfile --target moderation -t nl-moderation .
docker build -f docker/Dockerfile --target server -t nl-server .
```

Run headless server container (advanced):

```bash
docker run --rm -v nl-data:/data -e NL_DATA_ROOT=/data nl-server \
  --game generic --config /app/samples/configs/generic.nle \
  --source ws://0.0.0.0:27021/nl/v1 --nl-action auto
```

## Shared data layout

Under `NL_DATA_ROOT`:

| File | Purpose |
|------|---------|
| `moderation.jsonl` | Audit trail (Server + Moderation web) |
| `sp-profiles.json` | SP standing / offenses |
| `join-requirements.json` | Join gate |
| `session-profile.json` | Last Session Host profile |

Session Host web and Moderation web read/write the same files as `NL.Server` and the Windows
Moderation Console.

## Health checks

| Endpoint | Service |
|----------|---------|
| `GET /health` | Session Host web, Moderation web |

## Related docs

- [NL Deploy — CI/CD & public demo (Phase F)](NL_DEPLOY.md)
- [NL Session Bus (Phase B)](NL_SESSION_BUS.md)
- [NL Integration Spec v1](NL_INTEGRATION_SPEC.md)
- [Moderation tooling](MODERATION.md)
- [NLServer overview](NLSERVER.md)
