#!/usr/bin/env bash
# Hot-patch session-host wwwroot from GitHub main (no image rebuild).
set -euo pipefail
CID="$(docker ps -q -f name=session-host | head -1)"
if [[ -z "$CID" ]]; then
  CID="$(docker ps --format '{{.ID}} {{.Names}}' | awk '/session/ {print $1; exit}')"
fi
if [[ -z "$CID" ]]; then
  echo "ERROR: no session-host container" >&2
  docker ps >&2
  exit 1
fi
curl -fsSL "https://raw.githubusercontent.com/Surrplexie/NexoraLive/main/src/NL.SessionHost.Web/wwwroot/app.js" -o /tmp/app.js
curl -fsSL "https://raw.githubusercontent.com/Surrplexie/NexoraLive/main/src/NL.SessionHost.Web/wwwroot/operator.html" -o /tmp/operator.html
docker cp /tmp/app.js "$CID:/app/wwwroot/app.js"
docker cp /tmp/operator.html "$CID:/app/wwwroot/operator.html"
docker exec "$CID" ls -la /app/wwwroot/app.js /app/wwwroot/operator.html
echo "PATCHED_OK container=$CID — hard-refresh Operator (Ctrl+Shift+R)"
