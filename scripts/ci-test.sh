#!/usr/bin/env bash
# Run all cross-platform unit tests (CI).
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"

echo "=== NL CI test ==="
dotnet test "$repo/tests/NL.Core.Tests/NL.Core.Tests.csproj" -c Release --nologo
dotnet test "$repo/tests/NL.Server.Core.Tests/NL.Server.Core.Tests.csproj" -c Release --nologo
dotnet test "$repo/tests/NL.Server.Tests/NL.Server.Tests.csproj" -c Release --nologo
dotnet test "$repo/tests/NL.Moderation.Core.Tests/NL.Moderation.Core.Tests.csproj" -c Release --nologo
dotnet test "$repo/tests/NL.Moderation.Tests/NL.Moderation.Tests.csproj" -c Release --nologo
dotnet test "$repo/tests/NL.AntiCheat.Core.Tests/NL.AntiCheat.Core.Tests.csproj" -c Release --nologo
dotnet test "$repo/tests/NL.HotkeyDaemon.Core.Tests/NL.HotkeyDaemon.Core.Tests.csproj" -c Release --nologo

echo "CI tests OK."
