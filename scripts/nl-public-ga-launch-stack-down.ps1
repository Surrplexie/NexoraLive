# Phase 14 — stop public GA launch stack
param([switch]$RemoveVolumes)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "Stopping public GA launch stack..." -ForegroundColor Yellow
$args = @("compose", "-f", "docker/docker-compose.public-ga-launch.yml", "down")
if ($RemoveVolumes) { $args += "-v" }
& docker @args

Write-Host "Public GA launch stack stopped." -ForegroundColor Green
