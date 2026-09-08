#!/usr/bin/env bash
# Phase K — verify public demo hardening: ops status + admit rate limit returns 429.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
compose_file="$repo/docker/docker-compose.demo-local.yml"
health_url="${NL_HARDENING_SMOKE_HEALTH_URL:-http://127.0.0.1:27020/health}"
ops_url="${NL_HARDENING_SMOKE_OPS_URL:-http://127.0.0.1:27020/api/v1/ops/status}"
admit_url="${NL_HARDENING_SMOKE_ADMIT_URL:-http://127.0.0.1:27020/api/v1/session/admit}"
token="${NL_BUS_TOKEN:-hard-$(openssl rand -hex 16 2>/dev/null || echo hardbus1234567890)}"
op_key="${NL_OPERATOR_KEY:-hard-op-$(openssl rand -hex 8 2>/dev/null || echo operatorkey123)}"

cleanup() {
  docker compose -f "$compose_file" down -v --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT

export NL_BUS_TOKEN="$token"
export NL_OPERATOR_KEY="$op_key"
export NL_DEMO_RESET_INTERVAL_MINUTES="${NL_DEMO_RESET_INTERVAL_MINUTES:-0}"
export NL_DEMO_BRIDGE_INTERVAL="${NL_DEMO_BRIDGE_INTERVAL:-4}"
# Tight limits for smoke — must exceed burst in test below
export NL_HARDENING="true"
export NL_ADMIT_RATE_PER_MIN="${NL_ADMIT_RATE_PER_MIN:-5}"

echo "=== NL Phase K hardening smoke ==="
docker compose -f "$compose_file" up -d --build --wait

echo "Waiting for health …"
for i in $(seq 1 30); do
  if curl -fsS "$health_url" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

curl -fsS "$health_url"
echo ""

ops="$(curl -fsS "$ops_url")"
echo "Ops status: $ops"
if ! echo "$ops" | grep -q '"hardening"'; then
  echo "Missing hardening block in ops status." >&2
  exit 1
fi

echo "Probing admit rate limit (limit=${NL_ADMIT_RATE_PER_MIN}) …"
payload='{"playerId":"RateTest","displayName":"RateTest"}'
got_429=0
for i in $(seq 1 20); do
  code="$(curl -s -o /dev/null -w "%{http_code}" -X POST "$admit_url" \
    -H "Content-Type: application/json" -d "$payload")"
  if [ "$code" = "429" ]; then
    got_429=1
    echo "Received 429 on attempt $i"
    break
  fi
done

if [ "$got_429" -ne 1 ]; then
  echo "Expected 429 from admit rate limiter." >&2
  exit 1
fi

echo "Phase K hardening smoke passed."
exit 0
