#!/usr/bin/env bash
# Publish Linux-friendly NL headless + web operator apps (Phase C).
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
out="$repo/artifacts/publish-linux"
rm -rf "$out"
mkdir -p "$out"

publish() {
  local project="$1"
  local name="$2"
  echo "Publishing $name → $out/$name"
  dotnet publish "$repo/$project" -c Release -r linux-x64 --self-contained false -o "$out/$name"
}

publish "src/NL.Server/NL.Server.csproj" "Server"
publish "src/NL.SessionHost.Web/NL.SessionHost.Web.csproj" "SessionHostWeb"
publish "src/NL.Moderation.Web/NL.Moderation.Web.csproj" "ModerationWeb"

echo ""
echo "Publish complete: $out"
echo "Requires .NET 8 runtime on the target host."
echo "Set NL_DATA_ROOT to a writable directory (default: ~/.local/share/NL on Linux)."
