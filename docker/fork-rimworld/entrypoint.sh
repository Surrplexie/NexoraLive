#!/bin/sh
# NL RimWorld fork entrypoint (Phase T1).
# Dual-mode:
#   1) Licensed game tree mounted at /game → inject NL Harmony mod and exec dedicated.
#   2) Otherwise → C# sidecar with rimworld.nle vocabulary (CI / dogfood).
set -eu

GAME_ROOT="${NL_RIMWORLD_GAME:-/game}"
CONNECT_PORT="${NL_FORK_CONNECT_PORT:-25555}"
export NL_FORK_GAME="${NL_FORK_GAME:-rimworld}"
export NL_FORK_CONNECT_PORT="$CONNECT_PORT"

inject_mod() {
  dest="$GAME_ROOT/Mods/NLBridge"
  mkdir -p "$dest"
  if [ -d /opt/nl-rimworld-mod ]; then
    cp -a /opt/nl-rimworld-mod/. "$dest/"
  fi
}

find_dedicated() {
  for candidate in \
    "$GAME_ROOT/Start_Dedicated.sh" \
    "$GAME_ROOT/RimWorldLinux" \
    "$GAME_ROOT/RimWorldTogetherDedicated" \
    "$GAME_ROOT/DedicatedServer"
  do
    if [ -x "$candidate" ]; then
      echo "$candidate"
      return 0
    fi
  done
  return 1
}

if [ "${NL_RIMWORLD_DEDICATED:-}" = "1" ] || [ -d "$GAME_ROOT/Data" ] || [ -f "$GAME_ROOT/RimWorldLinux" ]; then
  if binary=$(find_dedicated); then
    inject_mod
    echo "[rimworld] dedicated mode → $binary (NL mod injected)"
    exec "$binary"
  fi
  echo "[rimworld] /game present but no dedicated binary — falling back to sidecar" >&2
fi

echo "[rimworld] sidecar mode (no licensed dedicated binary)"
cd /app
exec dotnet NL.Fork.Runtime.dll --game rimworld --connect-port "$CONNECT_PORT" "$@"
