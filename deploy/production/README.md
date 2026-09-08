# Production fleet deploy — VPS (Docker) or Kubernetes cluster

## Single-node VPS (Docker orchestrator)

Recommended for first production host. Session host runs in Docker with host `docker.sock` to spawn real fork containers.

```powershell
cd NexoraLive
powershell -File scripts/nl-production-stack-down.ps1
powershell -File scripts/nl-production-stack-up.ps1 -Validate
```

Expected: `PRODUCTION VALIDATION PASSED`

Copy [`samples/fleet/production.env.example`](../../samples/fleet/production.env.example), set real domains and secrets, and merge into `docker/production-fleet.env` (stack-up auto-writes the workspace host path).

## Kubernetes cluster (multi-node)

Manifests: [`deploy/k8s/production/`](../k8s/production/)

```bash
kubectl apply -f deploy/k8s/production/
kubectl -n nl-fleet-production set env deployment/nl-session-host \
  NL_FORK_DOCKER_IMAGE=your-registry/nl-fork-hello:latest \
  NL_PUBLIC_BASE_URL=https://nl.yourdomain.com
```

Build and push images first:

```bash
docker build -f docker/Dockerfile --target session-host -t your-registry/nl-session-host:latest .
docker build -f docker/fork-hello/Dockerfile -t your-registry/nl-fork-hello:latest .
docker push your-registry/nl-session-host:latest
docker push your-registry/nl-fork-hello:latest
```

Edge / relay / TURN: use [`deploy/staging/README.md`](../staging/README.md) Caddy templates with production domains.

Full guide: [docs/NL_PRODUCTION_FLEET.md](../../docs/NL_PRODUCTION_FLEET.md)
