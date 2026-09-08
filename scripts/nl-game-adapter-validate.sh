#!/usr/bin/env bash
# Phase T0 — CI-friendly adapter validate.
# Always runs GameForkAdapter unit tests (checklist for all discovered manifests).
# If pwsh is available, also runs the full PowerShell file checks.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo"

echo "=== NL Phase T0 game adapter validate (bash) ==="

dotnet test "$repo/tests/NL.Fork.Core.Tests/NL.Fork.Core.Tests.csproj" -c Release \
  --filter "FullyQualifiedName~GameForkAdapter" --verbosity quiet

if command -v pwsh >/dev/null 2>&1; then
  if [[ "${1:-}" == "--all" ]] || [[ "${1:-}" == "-All" ]] || [[ -z "${1:-}" ]]; then
    pwsh -File "$repo/scripts/nl-game-adapter-validate.ps1" -All -SkipUnitTests
  else
    pwsh -File "$repo/scripts/nl-game-adapter-validate.ps1" -GameId "$1" -SkipUnitTests
  fi
else
  echo "pwsh not found — unit checklist only (OK for CI)"
fi

echo "Phase T0 game adapter validate OK"
