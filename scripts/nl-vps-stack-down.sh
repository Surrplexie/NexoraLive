#!/usr/bin/env bash
# Stop VPS production stack
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
compose_file="$repo/docker/docker-compose.vps-production.yml"
caddy_env="$repo/docker/.env.vps"

args=(compose -f "$compose_file")
if [[ -f "$caddy_env" ]]; then
  args+=(--env-file "$caddy_env")
fi
args+=(down)

docker "${args[@]}"

names=$(docker ps -a --filter "name=nl-fork-" --format "{{.Names}}" 2>/dev/null || true)
for n in $names; do
  docker rm -f "$n" 2>/dev/null || true
done

echo "VPS production stack stopped."
