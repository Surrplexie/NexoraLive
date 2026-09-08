# Live production deploy (Phase 7)

GA stack on the public internet with live Steam identity, HTTPS, relay, and TURN.

## Single-node VPS (Docker)

```bash
cp samples/fleet/live-production.env.example docker/live-production-fleet.env
# Set NL_OPERATOR_KEY, NL_BUS_TOKEN, STEAM_WEB_API_KEY, domains
# Set NL_LIVE_PRODUCTION_DEV=false, NL_GA_ALLOW_MOCK_IDENTITY=false
# Set NL_FORK_DOCKER_WORKSPACE_HOST_ROOT to host path of docker/live-production-data

docker compose -f docker/docker-compose.live-production.yml up -d --build
curl -fsS https://play.yourdomain.com/health
curl -fsS https://play.yourdomain.com/api/v1/live-production/status
```

Local validation (dev mode):

```powershell
powershell -File scripts/nl-live-production-stack-up.ps1 -Validate
```

## Caddy (Let's Encrypt)

Replace `docker/edge-production/Caddyfile`:

```caddy
play.yourdomain.com {
	encode gzip
	reverse_proxy session-host:27020
}
```

Replace `docker/relay-production/Caddyfile`:

```caddy
relay-us-east.yourdomain.com {
	@fork path /fork/*
	handle @fork {
		uri strip_prefix /fork
		reverse_proxy session-host:27021
	}
}
```

Add `relay-us-west` and `relay-eu-west` blocks matching `NL_FLEET_RELAY_WS_TEMPLATE`.

## Kubernetes

```bash
kubectl create secret generic nl-live-production-secrets -n nl-live-production \
  --from-literal=steam-web-api-key=... \
  --from-literal=operator-key=... \
  --from-literal=bus-token=...
kubectl apply -f deploy/k8s/live-production/
```

## Validation

```powershell
powershell -File scripts/nl-live-production-validate.ps1 `
  -BaseUrl https://play.yourdomain.com `
  -HttpsBaseUrl https://play.yourdomain.com `
  -OperatorKey "..."
```

Full guide: [docs/NL_LIVE_PRODUCTION.md](../../docs/NL_LIVE_PRODUCTION.md)
