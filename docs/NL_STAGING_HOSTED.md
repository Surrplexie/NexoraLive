# NL Hosted Staging — Phase 3 Foundation

Run the NL control plane like a mini production host: **HTTPS edge**, **relay stub**, **TURN**, and **100-session validation gate**.

## Local hosted staging (Docker Desktop)

### One command — stack + validation

```powershell
cd C:\Users\surrp\Documents\GitHub\NexoraLive

# Stop any local `dotnet run` session host first (port 27020)
powershell -File scripts/nl-staging-stack-down.ps1

powershell -File scripts/nl-staging-stack-up.ps1 -Validate
```

Expected: `STAGING VALIDATION PASSED`

### Stack only (no load test)

```powershell
powershell -File scripts/nl-staging-stack-up.ps1
powershell -File scripts/nl-staging-validate.ps1
```

Validation uses the in-container NLE path `/app/samples/configs/fork-hello.nle` (bundled in the session-host image). For a local `dotnet run` host, pass your host path: `-NlePath "samples\configs\fork-hello.nle"`.

### Teardown

```powershell
powershell -File scripts/nl-staging-stack-down.ps1
# optional: -RemoveVolumes to wipe /data
```

## What the stack includes

| Service | Port | Purpose |
|---------|------|---------|
| **session-host** | 27020 HTTP, 27021 WS | NL Session Host + fleet ops |
| **edge** (Caddy) | 443 HTTPS, 80 HTTP | Public API surface (`https://127.0.0.1/health`) |
| **relay-stub** (Caddy) | 8443 WSS | Masks fork connect URLs (`wss://127.0.0.1:8443/fork/{session}`) |
| **coturn** | 3478 UDP/TCP | TURN for NAT traversal |

Compose file: [`docker/docker-compose.staging-fleet.yml`](../docker/docker-compose.staging-fleet.yml)

Env reference: [`samples/fleet/staging.env.example`](../samples/fleet/staging.env.example)

## Validation gate

The load test creates **100 mock fork sessions**, runs an **admit burst**, and checks SLOs:

| Check | Target |
|-------|--------|
| 100+ concurrent fork sessions | ≥100 active |
| Admit success rate | ≥99% |
| Fork create p99 | ≤5000 ms |
| Relay configured | non-empty template |
| Relay not placeholder | `127.0.0.1:8443` or `NL_FLEET_STAGING_DEV=true` |
| TURN configured | coturn on 3478 |

API: `GET /api/v1/fleet/validation` · UI: `/fleet-ops.html`

## VPS / public domain (production-like)

### 1. Provision server

- Ubuntu 22.04+ VPS, Docker + Compose
- DNS: `staging.yourdomain.com` → VPS IP
- DNS: `relay-us-east.staging.yourdomain.com` → VPS IP (or wildcard `*.staging.yourdomain.com`)

### 2. Clone repo and build

```bash
git clone https://github.com/yourorg/NexoraLive.git
cd NexoraLive
docker compose -f docker/docker-compose.staging-fleet.yml build
```

### 3. Create `.env` on the VPS

Copy [`samples/fleet/staging.env.example`](../samples/fleet/staging.env.example) and set:

```bash
NL_PUBLIC_BASE_URL=https://staging.yourdomain.com
NL_FLEET_RELAY_WS_TEMPLATE=wss://relay-{region}.staging.yourdomain.com/fork/{session}
NL_FLEET_TURN_URI=turn:turn.staging.yourdomain.com:3478?transport=udp
STEAM_WEB_API_KEY=your-key
NL_OWNERSHIP_MODE=live
NL_OPERATOR_KEY=long-random-secret
```

Replace the **edge** and **relay-stub** Caddy configs with real Let's Encrypt certs (see [`deploy/staging/Caddyfile.vps`](../deploy/staging/Caddyfile.vps)).

### 4. Reverse proxy pattern

```text
Internet → Caddy (TLS) → session-host:27020
         → relay Caddy  → session-host:27021 (WebSocket upgrade)
         → coturn:3478  (UDP/TCP)
```

Steam OpenID callback must match `NL_PUBLIC_BASE_URL` exactly.

### 5. Validate on VPS

```bash
curl -fsS https://staging.yourdomain.com/health
powershell -File scripts/nl-staging-validate.ps1 -BaseUrl https://staging.yourdomain.com
```

For HTTPS with self-signed local certs, use `-BaseUrl http://127.0.0.1:27020` on the host loopback.

## Kubernetes staging

Manifests: [`deploy/k8s/staging/`](../deploy/k8s/staging/)

```bash
kubectl apply -f deploy/k8s/staging/
kubectl -n nl-fleet-staging port-forward svc/nl-session-host 27020:27020
powershell -File scripts/nl-staging-validate.ps1
```

Set `NL_FORK_ORCHESTRATOR_MODE=kubernetes` in the ConfigMap for cluster fork Jobs.

## Secrets (never commit)

| Secret | Env var |
|--------|---------|
| Steam Web API | `STEAM_WEB_API_KEY` |
| Operator writes | `NL_OPERATOR_KEY` |
| TURN credentials | coturn `--user=` + client config |

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Port 27020 busy | Stop local `dotnet run` or old compose stack |
| Only ~30 forks created | Raise `NL_FLEET_FORK_CREATE_RATE_PER_MIN` (stack uses 200) |
| Hourly quota exceeded | `NL_FLEET_MAX_FORK_CREATES_PER_HOUR=9999` |
| Relay check fails | Set `NL_FLEET_RELAY_WS_TEMPLATE` to non-`example.com` URL or `NL_FLEET_STAGING_DEV=true` |
| HTTPS cert errors locally | Expected with Caddy `tls internal` — trust locally or use HTTP :27020 |

## Phase 3 exit criteria

- [x] Compose stack: session host + relay + TURN + HTTPS edge
- [x] Non-placeholder relay URL in staging env
- [x] `scripts/nl-staging-stack-up.ps1` + validation wrapper
- [x] 100-session validation PASS on mock orchestrator
- [ ] VPS with real domain + Let's Encrypt (operator deploy step)

Next: **Phase 4** — see [NL_PRODUCTION_FLEET.md](NL_PRODUCTION_FLEET.md) for real Docker/K8s forks and `NL_FLEET_PRODUCTION_READY=true`.

See also: [NL_FLEET_STAGING.md](NL_FLEET_STAGING.md) · [NL_FLEET_OPS.md](NL_FLEET_OPS.md)
