#!/bin/sh
set -eu

echo "[demo-bridge] waiting for NL demo session …"
attempt=0
while [ "$attempt" -lt 90 ]; do
  if curl -fsS "${NL_BRIDGE_WAIT_URL}" 2>/dev/null | grep -q '"sessionRunning":true'; then
    echo "[demo-bridge] session is running."
    break
  fi
  attempt=$((attempt + 1))
  sleep 2
done

if [ "$attempt" -ge 90 ]; then
  echo "[demo-bridge] warning: session not confirmed running; connecting anyway …" >&2
fi

exec python /app/nl_bridge.py \
  --url "${NL_BRIDGE_URL}" \
  --admit-url "${NL_ADMIT_URL}" \
  --loop \
  --interval "${NL_BRIDGE_INTERVAL}" \
  --reconnect-delay "${NL_BRIDGE_RECONNECT_DELAY}"
