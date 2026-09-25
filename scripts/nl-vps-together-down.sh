#!/usr/bin/env bash
# Stop RimWorld Together dedicated started by nl-vps-together-up.sh
set -euo pipefail

TOGETHER_ROOT="${NL_TOGETHER_ROOT:-/opt/rimworld-together}"

if [[ -f "$TOGETHER_ROOT/together.pid" ]]; then
  pid="$(cat "$TOGETHER_ROOT/together.pid")"
  if kill -0 "$pid" 2>/dev/null; then
    kill "$pid" || true
    sleep 1
    kill -9 "$pid" 2>/dev/null || true
    echo "Stopped PID $pid"
  fi
  rm -f "$TOGETHER_ROOT/together.pid"
fi

# Fallback: kill known binary path
if [[ -f "$TOGETHER_ROOT/.nl-together-bin" ]]; then
  bin="$(cat "$TOGETHER_ROOT/.nl-together-bin")"
  pkill -f "$bin" 2>/dev/null || true
fi

echo "Together dedicated stopped. (NL session-host untouched.)"
echo "To bring NL sidecar fork back: Operator Start with fork orchestrator ON."
