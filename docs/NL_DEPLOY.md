# NL Deploy — CI/CD & public demo (Phase F)

Phase F adds automated build/test, container publishing, and a one-command public demo stack
with TLS termination and persistent storage.

## Components

| Asset | Purpose |
|-------|---------|
| [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) | Build + test + Docker smoke on every PR/push |
| [`.github/workflows/docker.yml`](../.github/workflows/docker.yml) | Push `session-server` image to GHCR |
| [`docker/docker-compose.demo.yml`](../docker/docker-compose.demo.yml) | Public demo: Caddy + session server + volume |
| [`docker/Caddyfile`](../docker/Caddyfile) | TLS + `/nl/v1` WebSocket reverse proxy |
| [`scripts/deploy-demo.sh`](../scripts/deploy-demo.sh) | One-command demo deploy |
| [`docker/.env.demo.example`](../docker/.env.demo.example) | Demo env template |

## CI (GitHub Actions)

Every push/PR to `main`:

1. **Build** cross-platform projects (`NL.SessionHost.Web`, tests, etc.)
2. **Test** all 226+ unit tests
3. **Docker smoke** — build session-server compose stack, hit `/health`

On push to `main` (and version tags), the **Docker** workflow also publishes:

```
ghcr.io/<owner>/nexoralive/session-server:latest
ghcr.io/<owner>/nexoralive/session-server:<git-sha>
```

Enable GHCR package visibility in GitHub repo settings if you want public pulls.

### Run CI locally

```bash
bash scripts/ci-build.sh
bash scripts/ci-test.sh
bash scripts/docker-smoke.sh
```

## Deploy options

### Option A — Local / LAN (no TLS)

```bash
docker compose -f docker/docker-compose.session-server.yml up -d --build
curl -fsS http://127.0.0.1:27020/health
```

### Option B — Public demo with Caddy TLS (recommended)

**On a VPS** (Ubuntu 22.04+, Docker + Compose v2):

```bash
git clone https://github.com/Surrplexie/NexoraLive.git
cd NexoraLive

cp docker/.env.demo.example docker/.env
# Edit docker/.env:
#   NL_DEMO_DOMAIN=demo.yourdomain.com
#   NL_BUS_TOKEN=$(openssl rand -hex 32)
#   NL_OPERATOR_KEY=$(openssl rand -hex 32)
#   CADDY_ACME_EMAIL=you@yourdomain.com

bash scripts/deploy-demo.sh
```

Verify:

```bash
curl -fsS https://demo.yourdomain.com/health
curl -fsS https://demo.yourdomain.com/api/v1/security
```

Open `https://demo.yourdomain.com/` — the **Phase G demo loop** auto-starts a session and feeds sample events. Enter your operator key to control sessions or view bridge secrets.

Verify the live loop:

```bash
curl -fsS https://demo.yourdomain.com/api/v1/demo/status
# expect sessionRunning:true and decisions > 0 after ~30s
```

**DNS:** point `A`/`AAAA` record for `NL_DEMO_DOMAIN` to the VPS IP. Open ports **80** and **443**.

### Option C — Pull pre-built image from GHCR

In `docker/.env`:

```env
NL_SESSION_IMAGE=ghcr.io/surrplexie/nexoralive/session-server:latest
```

Then run `deploy-demo.sh` (skips local image build if image exists).

## Architecture (demo stack)

```text
Internet
   │
   ▼
Caddy :443 / :80  ── TLS termination
   │
   ├── /nl/v1*  ──► session-server:27021  (WebSocket bridge)
   └── /*       ──► session-server:27020  (HTTP dashboard + REST)
                         ▲
                         │ ws://session-server:27021 (internal)
                   demo-bridge (Phase G — loops sample NDJSON events)
                         │
                         └── volume nl-demo-data → /data
```

Environment inside the session server (set by compose):

| Variable | Example |
|----------|---------|
| `NL_PUBLIC_HTTP` | `https://demo.yourdomain.com` |
| `NL_PUBLIC_WS` | `wss://demo.yourdomain.com/nl/v1` |
| `NL_PUBLIC_MOD_HTTP` | `https://demo.yourdomain.com` |
| `NL_CORS_ORIGINS` | `https://demo.yourdomain.com` |
| `NL_PUBLIC_MODE` | `true` |
| `NL_BUS_TOKEN` | fixed bridge secret |
| `NL_OPERATOR_KEY` | operator REST secret |

See [NL_DEMO_SECURITY.md](NL_DEMO_SECURITY.md) for auth details.

## Persistent data

Demo compose mounts **`nl-demo-data`** at `/data` (`NL_DATA_ROOT`):

- SP profiles, moderation audit log, session profile survive container restarts
- To reset demo data: `docker compose -f docker/docker-compose.demo.yml down -v`

## Health checks

| Endpoint | Expected |
|----------|----------|
| `GET /health` | `{"status":"ok","service":"nl-session-server"}` |
| `GET /api/v1/security` | `operatorAuthRequired: true` in public mode |
| `GET /api/v1/demo/status` | Phase G: `enabled`, `sessionRunning`, `decisions` |

Docker images and compose services include built-in healthchecks on `/health`.

## Windows deploy

```powershell
Copy-Item docker\.env.demo.example docker\.env
# Edit docker\.env with secrets + domain
powershell -File scripts\deploy-demo.ps1
```

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Caddy won't get certificate | DNS points to server, ports 80/443 open, `CADDY_ACME_EMAIL` set |
| `401` on session start | Set operator key in dashboard or `X-NL-Operator-Key` header |
| Bridge can't connect | Use full `wss://domain/nl/v1?token=NL_BUS_TOKEN` from operator-authenticated manifest |
| Health never passes | `docker compose logs session-server` — often missing Phase E secrets when `NL_PUBLIC_MODE=true` |

## Related docs

- [NL Hosted Demo Loop (Phase G)](NL_DEMO.md)
- [NL Demo Security (Phase E)](NL_DEMO_SECURITY.md)
- [NL Session Server (Phase D)](NL_SESSION_SERVER.md)
- [Headless Linux / Docker](NL_HEADLESS_LINUX.md)
