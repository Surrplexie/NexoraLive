# Terminal 1 — Session Host for Docker dogfood (Phase 1.1–1.2)
param(
    [ValidateSet("mock", "live")]
    [string]$OwnershipMode = "mock",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$env:NL_FLEET_ENABLED = "true"
$env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
$env:NL_FLEET_MAX_FORK_CREATES_PER_HOUR = "999"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "docker"
$env:NL_IDENTITY_ENABLED = "true"
$env:NL_OWNERSHIP_MODE = $OwnershipMode
$env:NL_PUBLIC_BASE_URL = "http://127.0.0.1:27020"

if ($OwnershipMode -eq "live" -and -not $env:STEAM_WEB_API_KEY) {
    Write-Host "Set STEAM_WEB_API_KEY before -OwnershipMode live" -ForegroundColor Yellow
    Write-Host '  $env:STEAM_WEB_API_KEY = "your-key"' -ForegroundColor DarkGray
    exit 1
}

Write-Host "Starting Session Host (docker dogfood, ownership=$OwnershipMode)..." -ForegroundColor Cyan

if ($NoBuild) {
    dotnet run --project src/NL.SessionHost.Web -c Release --no-build
} else {
    dotnet run --project src/NL.SessionHost.Web -c Release
}
