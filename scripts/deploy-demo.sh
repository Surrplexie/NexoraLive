#!/usr/bin/env bash
# Deploy NL public demo stack (Phase F): Caddy TLS + session server + persistent volume.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
compose_file="$repo/docker/docker-compose.demo.yml"
env_file="$repo/docker/.env"

if [[ ! -f "$env_file" ]]; then
  echo "Missing $env_file — copy docker/.env.demo.example to docker/.env and set secrets." >&2
  exit 1
fi

# shellcheck disable=SC1090
source "$env_file"

domain="${NL_DEMO_DOMAIN:-localhost}"
if [[ "$domain" == "localhost" || "$domain" == "127.0.0.1" ]]; then
  export NL_PUBLIC_HTTP="${NL_PUBLIC_HTTP:-http://localhost}"
  export NL_PUBLIC_WS="${NL_PUBLIC_WS:-ws://localhost/nl/v1}"
  export NL_PUBLIC_MOD_HTTP="${NL_PUBLIC_MOD_HTTP:-http://localhost}"
  export NL_CORS_ORIGINS="${NL_CORS_ORIGINS:-http://localhost}"
  health_url="http://localhost/health"
else
  export NL_PUBLIC_HTTP="${NL_PUBLIC_HTTP:-https://${domain}}"
  export NL_PUBLIC_WS="${NL_PUBLIC_WS:-wss://${domain}/nl/v1}"
  export NL_PUBLIC_MOD_HTTP="${NL_PUBLIC_MOD_HTTP:-https://${domain}}"
  export NL_CORS_ORIGINS="${NL_CORS_ORIGINS:-https://${domain}}"
  health_url="https://${domain}/health"
fi

if [[ -z "${NL_BUS_TOKEN:-}" || "$NL_BUS_TOKEN" == replace-* ]]; then
  echo "Set NL_BUS_TOKEN in docker/.env (openssl rand -hex 32)." >&2
  exit 1
fi
if [[ -z "${NL_OPERATOR_KEY:-}" || "$NL_OPERATOR_KEY" == replace-* ]]; then
  echo "Set NL_OPERATOR_KEY in docker/.env (openssl rand -hex 32)." >&2
  exit 1
fi

echo "=== NL demo deploy ==="
echo "Domain:      $domain"
echo "Public HTTP: $NL_PUBLIC_HTTP"
echo "Public WS:   $NL_PUBLIC_WS"

docker compose -f "$compose_file" --env-file "$env_file" up -d --build --wait

echo "Waiting for edge health ..."
for i in $(seq 1 45); do
  if curl -kfsS "$health_url" >/dev/null 2>&1; then
    echo "Demo is up: $health_url"
    curl -kfsS "$health_url"
    echo ""
    echo "Dashboard: ${NL_PUBLIC_HTTP}/"
    echo "Moderation: ${NL_PUBLIC_MOD_HTTP}/moderation.html"
    exit 0
  fi
  sleep 2
done

echo "Edge health check failed: $health_url" >&2
docker compose -f "$compose_file" --env-file "$env_file" logs --tail 50
exit 1
