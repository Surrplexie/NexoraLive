#!/usr/bin/env bash
# Build NL fork Docker images (bash port of build-fork-images.ps1)
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo"

images="${1:-hello-fork}"
no_cache=""
if [[ "${2:-}" == "--no-cache" ]]; then no_cache="--no-cache"; fi

build_one() {
  local name="$1" dockerfile="$2" tag="$3"
  echo "=== Building ${name} (${tag}) ==="
  docker build $no_cache -f "$dockerfile" -t "$tag" .
  echo "OK: ${tag}"
}

IFS=',' read -ra targets <<< "$images"
for t in "${targets[@]}"; do
  case "$t" in
    hello-fork) build_one hello-fork docker/fork-hello/Dockerfile nl-fork-hello:latest ;;
    minecraft) build_one minecraft docker/fork-minecraft/Dockerfile nl-fork-minecraft:latest ;;
    minecraft-paper) build_one minecraft-paper docker/fork-minecraft-paper/Dockerfile nl-fork-minecraft-paper:latest ;;
    beamng) build_one beamng docker/fork-beamng/Dockerfile nl-fork-beamng:latest ;;
    all)
      build_one hello-fork docker/fork-hello/Dockerfile nl-fork-hello:latest
      build_one minecraft docker/fork-minecraft/Dockerfile nl-fork-minecraft:latest
      build_one beamng docker/fork-beamng/Dockerfile nl-fork-beamng:latest
      ;;
    *) echo "Unknown image: $t" >&2; exit 1 ;;
  esac
done

echo "Fork images built."
