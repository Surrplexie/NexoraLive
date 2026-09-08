#!/usr/bin/env bash
# Build the session-server image and verify /health responds.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
compose_file="$repo/docker/docker-compose.session-server.yml"
health_url="${NL_SMOKE_HEALTH_URL:-http://127.0.0.1:27020/health}"
token="${NL_BUS_TOKEN:-smoke-$(openssl rand -hex 16 2>/dev/null || echo smokebus1234567890)}"

cleanup() {
  docker compose -f "$compose_file" down -v --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "=== NL Docker smoke ==="
export NL_BUS_TOKEN="$token"

docker compose -f "$compose_file" up -d --build --wait

echo "Waiting for health ..."
for i in $(seq 1 30); do
  if curl -fsS "$health_url" >/dev/null 2>&1; then
    echo "Health OK: $health_url"
    curl -fsS "$health_url"
    echo ""
    manifest="$(curl -fsS http://127.0.0.1:27020/api/v1/session/manifest)"
    echo "Manifest fetched ($(echo "$manifest" | wc -c) bytes)"
    exit 0
  fi
  sleep 2
done

echo "Health check failed: $health_url" >&2
docker compose -f "$compose_file" logs --tail 40
exit 1
