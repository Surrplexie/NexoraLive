#!/usr/bin/env bash
# Patch VPS fleet env for Path A live admit (Steam). Does NOT set Twitch secrets.
# Run on the VPS: sudo bash scripts/nl-vps-live-admit-cutover.sh
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
fleet="$repo/docker/vps-production-fleet.env"
domain="${NL_LIVE_DOMAIN:-play.20062006.xyz}"

if [[ ! -f "$fleet" ]]; then
  echo "Missing $fleet — run nl-vps-init-env / bootstrap first." >&2
  exit 1
fi

upsert() {
  local key="$1" val="$2"
  if grep -q "^${key}=" "$fleet"; then
    sed -i "s|^${key}=.*|${key}=${val}|" "$fleet"
  else
    printf '\n%s=%s\n' "$key" "$val" >> "$fleet"
  fi
}

upsert NL_PUBLIC_BASE_URL "https://${domain}"
upsert NL_PUBLIC_HTTP "https://${domain}"
upsert NL_PUBLIC_WS "wss://${domain}/nl/v1"
upsert NL_PUBLIC_HOST "$domain"
upsert NL_FORK_PUBLIC_CONNECT_HOST "$domain"
upsert NL_OWNERSHIP_MODE live
upsert NL_SOCIAL_MODE live
upsert NL_FLEET_MIN_TWITCH_FOLLOWERS 0
# Load-test friendly (Path A public ready). Dial back to 30 / 6 after PUBLIC READY.
upsert NL_FLEET_FORK_CREATE_RATE_PER_MIN 200
upsert NL_FLEET_MAX_FORK_CREATES_PER_HOUR 9999

if ! grep -q '^STEAM_WEB_API_KEY=.\+' "$fleet"; then
  echo "WARN: STEAM_WEB_API_KEY empty — paste a real key into $fleet before recreate." >&2
fi

echo "Updated $fleet for live admit (followers=0)."
echo "Next:"
echo "  1. Confirm STEAM_WEB_API_KEY is set"
echo "  2. sudo docker compose -f docker/docker-compose.vps-production.yml --env-file docker/.env.vps up -d --force-recreate session-host"
echo "  3. curl -fsS https://${domain}/api/v1/identity/settings"
echo "  4. Follow docs/NL_LIVE_ADMIT.md Part 2–3 (profile 294100 + join)"
