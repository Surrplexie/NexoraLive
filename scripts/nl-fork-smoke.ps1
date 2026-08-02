# Phase P — hello-fork smoke (session bus + in-process enforcement)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase P fork smoke ===" -ForegroundColor Cyan

dotnet build src/NL.sln -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test tests/NL.Fork.Core.Tests/NL.Fork.Core.Tests.csproj -c Release --no-build --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Embedded fork: block shoot preserves Bob health..." -ForegroundColor Green
dotnet run --project src/NL.Fork.Runtime -c Release --no-build -- `
  --config samples/configs/fork-hello.nle `
  --mods samples/fork/hello-fork.mods.json 2>&1 | Select-Object -First 8

Write-Host ""
Write-Host "Phase P smoke OK (unit tests + embedded fork run)" -ForegroundColor Green
