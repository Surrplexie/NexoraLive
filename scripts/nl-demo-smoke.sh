#!/usr/bin/env bash
# Phase G — verify demo loop: auto-started session + sidecar bridge emits decisions.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
compose_file="$repo/docker/docker-compose.demo-local.yml"
health_url="${NL_DEMO_SMOKE_HEALTH_URL:-http://127.0.0.1:27020/health}"
demo_url="${NL_DEMO_SMOKE_DEMO_URL:-http://127.0.0.1:27020/api/v1/demo/status}"
token="${NL_BUS_TOKEN:-demo-$(openssl rand -hex 16 2>/dev/null || echo demobus1234567890)}"
op_key="${NL_OPERATOR_KEY:-demo-op-$(openssl rand -hex 8 2>/dev/null || echo operatorkey123)}"

cleanup() {
  docker compose -f "$compose_file" down -v --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT

export NL_BUS_TOKEN="$token"
export NL_OPERATOR_KEY="$op_key"
export NL_DEMO_RESET_INTERVAL_MINUTES="${NL_DEMO_RESET_INTERVAL_MINUTES:-0}"
export NL_DEMO_BRIDGE_INTERVAL="${NL_DEMO_BRIDGE_INTERVAL:-3}"

echo "=== NL Phase G demo smoke ==="
docker compose -f "$compose_file" up -d --build --wait

echo "Waiting for demo session …"
for i in $(seq 1 45); do
  demo_json="$(curl -fsS "$demo_url" 2>/dev/null || true)"
  if echo "$demo_json" | grep -q '"sessionRunning":true'; then
    echo "Demo session running."
    break
  fi
  sleep 2
done

if ! curl -fsS "$demo_url" | grep -q '"sessionRunning":true'; then
  echo "Demo session did not start." >&2
  docker compose -f "$compose_file" logs --tail 60
  exit 1
fi

echo "Waiting for decisions from demo bridge …"
for i in $(seq 1 40); do
  decisions="$(curl -fsS "$demo_url" | sed -n 's/.*"decisions":\([0-9]*\).*/\1/p')"
  if [ -n "${decisions:-}" ] && [ "$decisions" -gt 0 ] 2>/dev/null; then
    echo "Decisions recorded: $decisions"
    curl -fsS "$health_url"
    echo ""
    curl -fsS "$demo_url"
    echo ""
    curl -fsS "http://127.0.0.1:27020/api/v1/spectator/status"
    echo ""
    curl -fsS "http://127.0.0.1:27020/api/v1/spectator/scenarios" | head -c 200
    echo ""
    exit 0
  fi
  sleep 2
done

echo "No decisions recorded — demo bridge may not be connected." >&2
docker compose -f "$compose_file" logs --tail 80
exit 1
