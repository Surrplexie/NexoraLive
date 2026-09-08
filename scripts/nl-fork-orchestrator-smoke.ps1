# Phase O fork orchestrator smoke
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase O fork orchestrator smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Fork.Orchestrator/NL.Fork.Orchestrator.csproj -c Release --verbosity quiet
dotnet test tests/NL.Fork.Orchestrator.Tests/NL.Fork.Orchestrator.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase O fork orchestrator smoke OK" -ForegroundColor Green
