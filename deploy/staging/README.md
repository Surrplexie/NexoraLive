# VPS staging deploy — Caddy + Let's Encrypt

Replace `docker/edge-staging/Caddyfile` and `docker/relay-stub/Caddyfile` on your VPS with real domains.

## edge-staging (session host API)

```caddy
staging.yourdomain.com {
	encode gzip
	reverse_proxy session-host:27020
}
```

## relay-stub (fork WebSocket relay)

```caddy
relay-us-east.staging.yourdomain.com {
	encode gzip
	@fork path /fork/*
	handle @fork {
		uri strip_prefix /fork
		reverse_proxy session-host:27021
	}
}
```

Add `relay-us-west` and `relay-eu-west` blocks (or wildcard `*.staging.yourdomain.com`) matching `NL_FLEET_RELAY_WS_TEMPLATE`.

## Environment on VPS

```bash
NL_PUBLIC_BASE_URL=https://staging.yourdomain.com
NL_FLEET_RELAY_WS_TEMPLATE=wss://relay-{region}.staging.yourdomain.com/fork/{session}
NL_FLEET_TURN_URI=turn:turn.staging.yourdomain.com:3478?transport=udp
NL_FLEET_STAGING_DEV=false
NL_FLEET_PRODUCTION_READY=false
NL_OPERATOR_KEY=<generate-strong-secret>
STEAM_WEB_API_KEY=<from-steam-partner>
NL_OWNERSHIP_MODE=live
```

## Deploy commands

```bash
cd NexoraLive
docker compose -f docker/docker-compose.staging-fleet.yml up -d --build
curl -fsS https://staging.yourdomain.com/health
```

Full guide: [docs/NL_STAGING_HOSTED.md](../../docs/NL_STAGING_HOSTED.md)
