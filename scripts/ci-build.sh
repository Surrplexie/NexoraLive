#!/usr/bin/env bash
# Cross-platform CI build (Linux/macOS agents — skips net8.0-windows projects).
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== NL CI build ==="
dotnet --info | head -n 5

projects=(
  "src/NL.Core/NL.Core.csproj"
  "src/NL.Server.Core/NL.Server.Core.csproj"
  "src/NL.Server/NL.Server.csproj"
  "src/NL.SessionHost.Web/NL.SessionHost.Web.csproj"
  "src/NL.Moderation.Web/NL.Moderation.Web.csproj"
  "src/NL.Simulator/NL.Simulator.csproj"
)

for project in "${projects[@]}"; do
  echo "Building $project ..."
  dotnet build "$repo/$project" -c Release --nologo -v q
done

echo "CI build OK."
