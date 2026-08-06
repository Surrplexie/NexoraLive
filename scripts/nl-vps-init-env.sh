#!/usr/bin/env bash
# Generate docker/vps-production-fleet.env and docker/.env.vps from prompts.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
fleet_env="$repo/docker/vps-production-fleet.env"
caddy_env="$repo/docker/.env.vps"
sample_fleet="$repo/samples/fleet/vps-production.env.example"
sample_caddy="$repo/docker/.env.vps.example"

rand_hex() { openssl rand -hex 16; }

read -rp "Play domain (e.g. play.example.com): " play_domain
read -rp "Base domain (e.g. example.com): " base_domain
read -rp "ACME email for Let's Encrypt: " acme_email
read -rp "Support contact email: " support_email
read -rp "Steam Web API key (optional, press Enter to skip): " steam_key

operator_key="$(rand_hex)"
bus_token="$(rand_hex)"
turn_secret="$(rand_hex)"

mkdir -p "$(dirname "$fleet_env")"
cp "$sample_fleet" "$fleet_env"
cp "$sample_caddy" "$caddy_env"

sed -i "s|play.yourdomain.com|${play_domain}|g" "$fleet_env" "$caddy_env"
sed -i "s|yourdomain.com|${base_domain}|g" "$fleet_env" "$caddy_env"
sed -i "s|you@yourdomain.com|${acme_email}|g" "$caddy_env"
sed -i "s|support@yourdomain.com|${support_email}|g" "$fleet_env"
sed -i "s|change-me-turn-secret|${turn_secret}|g" "$fleet_env" "$caddy_env"
sed -i "s|NL_OPERATOR_KEY=change-me|NL_OPERATOR_KEY=${operator_key}|g" "$fleet_env"
sed -i "s|NL_BUS_TOKEN=change-me|NL_BUS_TOKEN=${bus_token}|g" "$fleet_env"
if [[ -n "$steam_key" ]]; then
  sed -i "s|STEAM_WEB_API_KEY=|STEAM_WEB_API_KEY=${steam_key}|g" "$fleet_env"
fi

workspace="/var/lib/nl/vps-fork-workspace"
sudo mkdir -p "$workspace"
sudo chown -R "$(id -un)":"$(id -gn)" "$workspace" 2>/dev/null || true
sed -i "s|NL_FORK_DOCKER_WORKSPACE_HOST_ROOT=.*|NL_FORK_DOCKER_WORKSPACE_HOST_ROOT=${workspace}|g" "$fleet_env"

echo ""
echo "=== Generated VPS env files ==="
echo "  $fleet_env"
echo "  $caddy_env"
echo ""
echo "Operator key (save offline): $operator_key"
echo "Bus token:                   $bus_token"
echo ""
echo "DNS A/AAAA records required (all -> VPS IP):"
echo "  ${play_domain}"
echo "  relay-us-east.${base_domain}"
echo "  relay-us-west.${base_domain}"
echo "  relay-eu-west.${base_domain}"
echo "  turn.${base_domain}  (optional, for TURN hostname in docs)"
echo ""
echo "Next: bash scripts/nl-vps-deploy.sh"
