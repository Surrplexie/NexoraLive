#!/usr/bin/env bash
# Fail fast if VPS env is not actually production-ready.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
caddy_env="$repo/docker/.env.vps"
fleet_env="$repo/docker/vps-production-fleet.env"
ok=1

echo "=== NL VPS preflight ==="

need() {
  if [[ ! -f "$1" ]]; then
    echo "MISSING: $1" >&2
    ok=0
  fi
}
need "$caddy_env"
need "$fleet_env"
need "$repo/docker/docker-compose.vps-production.yml"
need "$repo/docker/edge-vps/Caddyfile"

if [[ -f "$caddy_env" ]]; then
  # shellcheck disable=SC1090
  source "$caddy_env"
  if [[ "${NL_VPS_DOMAIN:-}" == *yourdomain* || -z "${NL_VPS_DOMAIN:-}" ]]; then
    echo "FAIL: NL_VPS_DOMAIN still a placeholder" >&2
    ok=0
  else
    echo "OK: domain ${NL_VPS_DOMAIN}"
  fi
  if [[ "${CADDY_ACME_EMAIL:-}" == *yourdomain* || -z "${CADDY_ACME_EMAIL:-}" ]]; then
    echo "FAIL: CADDY_ACME_EMAIL" >&2
    ok=0
  fi
fi

if [[ -f "$fleet_env" ]]; then
  if grep -q 'NL_OPERATOR_KEY=change-me' "$fleet_env"; then
    echo "FAIL: NL_OPERATOR_KEY=change-me" >&2
    ok=0
  else
    echo "OK: operator key set"
  fi
  if grep -qE '^STEAM_WEB_API_KEY=\s*$' "$fleet_env"; then
    echo "WARN: STEAM_WEB_API_KEY empty — live joins will fail"
  else
    echo "OK: Steam Web API key present"
  fi
  if grep -q 'NL_FORK_PUBLIC_CONNECT_HOST=play.yourdomain.com' "$fleet_env" \
     || grep -q 'NL_FORK_PUBLIC_CONNECT_HOST=yourdomain' "$fleet_env"; then
    echo "FAIL: NL_FORK_PUBLIC_CONNECT_HOST still a placeholder" >&2
    ok=0
  elif grep -q '^NL_FORK_PUBLIC_CONNECT_HOST=' "$fleet_env"; then
    echo "OK: NL_FORK_PUBLIC_CONNECT_HOST set"
  else
    echo "FAIL: NL_FORK_PUBLIC_CONNECT_HOST missing (T2-lite)" >&2
    ok=0
  fi
  if grep -qiE '^NL_PUBLIC_GA_LAUNCH_DEV=(true|1|yes)\s*$' "$fleet_env"; then
    echo "FAIL: fleet env must not force GA launch DEV on" >&2
    ok=0
  else
    echo "OK: GA launch DEV not forced on"
  fi
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "FAIL: docker not installed" >&2
  ok=0
else
  echo "OK: docker $(docker --version)"
fi

workspace="${NL_FORK_DOCKER_WORKSPACE_HOST_ROOT:-/var/lib/nl/vps-fork-workspace}"
if [[ ! -d "$workspace" ]]; then
  echo "WARN: workspace $workspace missing — init-env creates it"
else
  echo "OK: workspace $workspace"
fi

if [[ "$ok" -ne 1 ]]; then
  echo "PREFLIGHT FAILED" >&2
  exit 1
fi
echo "PREFLIGHT PASSED"
exit 0
