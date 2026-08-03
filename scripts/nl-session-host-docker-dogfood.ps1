# Terminal 1 — Session Host for Docker dogfood (Phase 1.1–1.2).
param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [switch]$Build
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
$env:NL_OWNERSHIP_MODE = "mock"

if ($Build) {
    dotnet build src/NL.SessionHost.Web -c $Configuration --verbosity quiet
}

$runArgs = @("--project", "src/NL.SessionHost.Web", "-c", $Configuration)
if (-not $Build) { $runArgs += "--no-build" }

Write-Host "Starting Session Host (docker dogfood env)..." -ForegroundColor Cyan
dotnet run @runArgs
