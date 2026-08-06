#!/usr/bin/env bash
# Deploy or upgrade VPS production stack
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
compose_file="$repo/docker/docker-compose.vps-production.yml"
caddy_env="$repo/docker/.env.vps"
fleet_env="$repo/docker/vps-production-fleet.env"

if [[ ! -f "$caddy_env" ]]; then
  echo "Missing $caddy_env — run: bash scripts/nl-vps-init-env.sh" >&2
  exit 1
fi
if [[ ! -f "$fleet_env" ]]; then
  echo "Missing $fleet_env — run: bash scripts/nl-vps-init-env.sh" >&2
  exit 1
fi

# shellcheck disable=SC1090
source "$caddy_env"

if [[ "$NL_VPS_DOMAIN" == *yourdomain* ]]; then
  echo "Edit docker/.env.vps — replace yourdomain.com placeholders." >&2
  exit 1
fi
if [[ -z "${CADDY_ACME_EMAIL:-}" || "$CADDY_ACME_EMAIL" == *yourdomain* ]]; then
  echo "Set CADDY_ACME_EMAIL in docker/.env.vps" >&2
  exit 1
fi
if grep -q 'NL_OPERATOR_KEY=change-me' "$fleet_env"; then
  echo "Run nl-vps-init-env.sh or set NL_OPERATOR_KEY in vps-production-fleet.env" >&2
  exit 1
fi

health_url="https://${NL_VPS_DOMAIN}/health"

echo "=== NL VPS deploy ==="
echo "Domain:  $NL_VPS_DOMAIN"
echo "Health:  $health_url"

echo "Building fork images..."
bash "$repo/scripts/build-fork-images.sh" hello-fork,minecraft,beamng

echo "Starting compose stack..."
docker compose -f "$compose_file" --env-file "$caddy_env" up -d --build --remove-orphans

echo "Waiting for TLS + health (up to 3 min)..."
for i in $(seq 1 90); do
  if curl -fsS "$health_url" >/dev/null 2>&1; then
    echo ""
    echo "VPS production is up."
    curl -fsS "$health_url"
    echo ""
    echo "Play:        https://${NL_VPS_DOMAIN}/play.html"
    echo "GA signup:   https://${NL_VPS_DOMAIN}/ga.html"
    echo "Status:      https://${NL_VPS_DOMAIN}/status.html"
    echo "Ops:         https://${NL_VPS_DOMAIN}/public-ga-launch-ops.html"
    echo ""
    echo "Validate from your PC:"
    echo "  powershell -File scripts/nl-vps-validate.ps1 -BaseUrl https://${NL_VPS_DOMAIN} -OperatorKey <key>"
    exit 0
  fi
  sleep 2
done

echo "Health check failed: $health_url" >&2
docker compose -f "$compose_file" --env-file "$caddy_env" logs --tail 80 caddy session-host
exit 1
