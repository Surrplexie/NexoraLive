#!/usr/bin/env bash
# Run headless NL.Server on Linux (generic game + WebSocket listen).
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
config="${NL_CONFIG:-$repo/samples/configs/generic.nle}"
source="${NL_SOURCE:-ws://127.0.0.1:27021/nl/v1}"
game="${NL_GAME:-generic}"

export NL_DATA_ROOT="${NL_DATA_ROOT:-$HOME/.local/share/NL}"
mkdir -p "$NL_DATA_ROOT"

echo "NL headless server"
echo "  data:   $NL_DATA_ROOT"
echo "  config: $config"
echo "  source: $source"
echo ""

dotnet run --project "$repo/src/NL.Server" -- \
  --game "$game" \
  --config "$config" \
  --source "$source" \
  --nl-action auto \
  "$@"
