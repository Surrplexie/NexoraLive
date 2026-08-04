# Phase 4 — session host on host with production fleet env (Docker provisioner; pairs with edge/relay compose)
param([switch]$NoBuild)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$env:NL_BIND = "0.0.0.0"
$env:NL_FLEET_ENABLED = "true"
$env:NL_FLEET_MAX_CONCURRENT = "128"
$env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
$env:NL_FLEET_FORK_CREATE_RATE_PER_MIN = "200"
$env:NL_FLEET_MAX_FORK_CREATES_PER_HOUR = "9999"
$env:NL_FLEET_STAGING_DEV = "false"
$env:NL_FLEET_PRODUCTION_READY = "true"
$env:NL_PUBLIC_BASE_URL = "https://127.0.0.1"
$env:NL_FLEET_RELAY_WS_TEMPLATE = "wss://127.0.0.1:8443/fork/{session}"
$env:NL_FLEET_TURN_URI = "turn:127.0.0.1:3478?transport=udp"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "docker"
$env:NL_FORK_DOCKER_IMAGE = "nl-fork-hello:latest"
$env:NL_IDENTITY_ENABLED = "true"
$env:NL_OWNERSHIP_MODE = "mock"

Write-Host "Building nl-fork-hello:latest if missing..." -ForegroundColor DarkGray
docker image inspect nl-fork-hello:latest 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    docker build -f docker/fork-hello/Dockerfile -t nl-fork-hello:latest .
}

Write-Host "Starting local session host (production fleet env, docker orchestrator)..." -ForegroundColor Cyan
if ($NoBuild) {
    dotnet run --project src/NL.SessionHost.Web -c Release --no-build
} else {
    dotnet run --project src/NL.SessionHost.Web -c Release
}
