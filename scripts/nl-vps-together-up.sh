#!/usr/bin/env bash
# Install / start RimWorld Together dedicated on the VPS (port 25555).
# Frees the NL sidecar fork binding that only speaks NL-RIMWORLD/1 banner.
#
# Usage (on VPS):
#   cd /opt/NexoraLive && sudo bash scripts/nl-vps-together-up.sh
# Stop:
#   sudo bash scripts/nl-vps-together-down.sh
set -euo pipefail

TOGETHER_ROOT="${NL_TOGETHER_ROOT:-/opt/rimworld-together}"
PORT="${NL_TOGETHER_PORT:-25555}"
# Prefer official Server-* asset; override with NL_TOGETHER_URL if needed.
RELEASE_TAG="${NL_TOGETHER_RELEASE:-26.8.31.1}"
DOWNLOAD_URL="${NL_TOGETHER_URL:-https://github.com/RimWorld-Together/Rimworld-Together/releases/download/${RELEASE_TAG}/Server-linux-x64.zip}"

echo "=== NL: RimWorld Together dedicated ==="
echo "Root: $TOGETHER_ROOT"
echo "Port: $PORT"
echo "Zip:  $DOWNLOAD_URL"

# 1) Free 25555 — stop NL rimworld sidecar forks (session host can stay up).
echo "Stopping nl-fork-* containers that hold :$PORT ..."
mapfile -t forks < <(docker ps --format '{{.Names}}' | grep -E '^nl-fork-' || true)
if ((${#forks[@]} > 0)); then
  docker stop "${forks[@]}" || true
  docker rm -f "${forks[@]}" 2>/dev/null || true
  echo "Stopped: ${forks[*]}"
else
  echo "No nl-fork-* containers running."
fi

# Anything else on the port?
if command -v ss >/dev/null 2>&1; then
  if ss -tlnp | grep -q ":${PORT} "; then
    echo "WARNING: something still listens on :$PORT — inspect with: ss -tlnp | grep $PORT" >&2
  fi
fi

# 2) Install server if missing
mkdir -p "$TOGETHER_ROOT"
cd "$TOGETHER_ROOT"
if [[ ! -f .nl-together-installed ]] || [[ "${NL_TOGETHER_FORCE_REINSTALL:-}" == "1" ]]; then
  echo "Downloading Together server..."
  tmp="$(mktemp -d)"
  curl -fsSL -o "$tmp/server.zip" "$DOWNLOAD_URL"
  rm -rf "$TOGETHER_ROOT"/*
  unzip -qo "$tmp/server.zip" -d "$TOGETHER_ROOT"
  rm -rf "$tmp"
  # Find executable (layout varies by release)
  bin=""
  for candidate in \
    "$TOGETHER_ROOT/GameServer" \
    "$TOGETHER_ROOT/Server" \
    "$TOGETHER_ROOT/RimworldTogetherServer" \
    "$TOGETHER_ROOT/linux-x64/GameServer"
  do
    if [[ -f "$candidate" ]]; then
      bin="$candidate"
      break
    fi
  done
  if [[ -z "$bin" ]]; then
    bin="$(find "$TOGETHER_ROOT" -maxdepth 3 -type f -executable \( -name 'GameServer' -o -name 'Server' -o -name '*Together*Server*' \) | head -1 || true)"
  fi
  if [[ -z "$bin" ]]; then
    echo "ERROR: no server binary found after unzip. Listing:" >&2
    find "$TOGETHER_ROOT" -maxdepth 2 -type f | head -40 >&2
    exit 1
  fi
  chmod +x "$bin"
  echo "$bin" > "$TOGETHER_ROOT/.nl-together-bin"
  date -u +%Y-%m-%dT%H:%M:%SZ > "$TOGETHER_ROOT/.nl-together-installed"
  echo "Installed binary: $bin"
else
  echo "Already installed (set NL_TOGETHER_FORCE_REINSTALL=1 to refresh)."
fi

bin="$(cat "$TOGETHER_ROOT/.nl-together-bin" 2>/dev/null || true)"
if [[ -z "$bin" || ! -x "$bin" ]]; then
  echo "ERROR: missing binary marker — reinstall with NL_TOGETHER_FORCE_REINSTALL=1" >&2
  exit 1
fi

# 3) Firewall
if command -v ufw >/dev/null 2>&1; then
  ufw allow "${PORT}/tcp" >/dev/null 2>&1 || true
fi

# 4) Start under systemd-run / nohup if not already up
if pgrep -f "$bin" >/dev/null 2>&1; then
  echo "Together server already running."
  pgrep -af "$bin" || true
  exit 0
fi

workdir="$(dirname "$bin")"
cd "$workdir"
logfile="$TOGETHER_ROOT/together.log"
echo "Starting Together server (cwd=$workdir) → log $logfile"
nohup "$bin" >>"$logfile" 2>&1 &
echo $! > "$TOGETHER_ROOT/together.pid"
sleep 2
if pgrep -f "$bin" >/dev/null 2>&1; then
  echo "OK: Together PID $(cat "$TOGETHER_ROOT/together.pid")"
  echo "Connect from RimWorld Together Direct Connect:"
  echo "  IP:   play.20062006.xyz   (or this VPS public IP)"
  echo "  Port: $PORT"
  echo "Tail log:  tail -f $logfile"
  echo "Stop:      sudo bash /opt/NexoraLive/scripts/nl-vps-together-down.sh"
else
  echo "ERROR: process exited — last log lines:" >&2
  tail -40 "$logfile" >&2 || true
  exit 1
fi
