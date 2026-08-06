#!/usr/bin/env bash
# First-time VPS bootstrap: Docker + repo + env + deploy
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== NL VPS bootstrap ==="

if ! command -v docker >/dev/null 2>&1; then
  echo "Installing Docker..."
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker "$USER" || true
  echo "Docker installed. Log out/in if 'docker' permission denied, then re-run."
fi

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose plugin required." >&2
  exit 1
fi

if [[ ! -f "$repo/docker/vps-production-fleet.env" ]]; then
  bash "$repo/scripts/nl-vps-init-env.sh"
fi

if command -v ufw >/dev/null 2>&1; then
  echo "Opening firewall ports 80, 443, 3478..."
  sudo ufw allow 80/tcp || true
  sudo ufw allow 443/tcp || true
  sudo ufw allow 3478/tcp || true
  sudo ufw allow 3478/udp || true
fi

bash "$repo/scripts/nl-vps-deploy.sh"
