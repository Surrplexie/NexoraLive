# Phase S fleet operations smoke + staging validation (unit tests + optional live run)
param(
    [switch]$Live,
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase S fleet ops smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Fleet/NL.Fleet.csproj -c Release --verbosity quiet
dotnet build src/NL.Fork.Orchestrator/NL.Fork.Orchestrator.csproj -c Release --verbosity quiet
dotnet test tests/NL.Fleet.Tests/NL.Fleet.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test tests/NL.Fork.Orchestrator.Tests/NL.Fork.Orchestrator.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Live) {
    Write-Host "Live staging validation against $BaseUrl ..." -ForegroundColor Yellow
    & "$Root\scripts\nl-fleet-staging-validation.ps1" -BaseUrl $BaseUrl -ConcurrentSessions 100 -AdmitBurst 30
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Phase S fleet ops smoke OK" -ForegroundColor Green
