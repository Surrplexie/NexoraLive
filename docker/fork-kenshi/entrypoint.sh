#!/bin/sh
# NL Kenshi fork entrypoint (Phase T1).
# Dual-mode:
#   1) Licensed game tree mounted at /game → inject NL hook catalog and exec community MP host.
#   2) Otherwise → C# sidecar with kenshi.nle vocabulary (CI / dogfood).
set -eu

GAME_ROOT="${NL_KENSHI_GAME:-/game}"
CONNECT_PORT="${NL_FORK_CONNECT_PORT:-23386}"
export NL_FORK_GAME="${NL_FORK_GAME:-kenshi}"
export NL_FORK_CONNECT_PORT="$CONNECT_PORT"

inject_mod() {
  dest="$GAME_ROOT/mods/NLBridge"
  mkdir -p "$dest"
  if [ -d /opt/nl-kenshi-mod ]; then
    cp -a /opt/nl-kenshi-mod/. "$dest/"
  fi
}

find_host() {
  for candidate in \
    "$GAME_ROOT/kenshi_mp" \
    "$GAME_ROOT/KenshiMP" \
    "$GAME_ROOT/kenshi" \
    "$GAME_ROOT/Kenshi"
  do
    if [ -x "$candidate" ]; then
      echo "$candidate"
      return 0
    fi
  done
  return 1
}

if [ "${NL_KENSHI_DEDICATED:-}" = "1" ] || [ -d "$GAME_ROOT/data" ] || [ -f "$GAME_ROOT/kenshi" ]; then
  if binary=$(find_host); then
    inject_mod
    echo "[kenshi] licensed host mode → $binary (NL mod injected)"
    exec "$binary"
  fi
  echo "[kenshi] /game present but no host binary — falling back to sidecar" >&2
fi

echo "[kenshi] sidecar mode (no licensed game binary)"
cd /app
exec dotnet NL.Fork.Runtime.dll --game kenshi --connect-port "$CONNECT_PORT" "$@"
