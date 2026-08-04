# Phase 4 — stop production fleet stack and orphan fork containers
param([switch]$RemoveVolumes)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "Stopping production stack..." -ForegroundColor Yellow
$args = @("compose", "-f", "docker/docker-compose.production-fleet.yml", "down")
if ($RemoveVolumes) { $args += "-v" }
& docker @args

Write-Host "Removing orphan nl-fork-* containers..." -ForegroundColor DarkGray
$names = @(docker ps -a --filter "name=nl-fork-" --format "{{.Names}}" 2>$null)
foreach ($n in $names) {
    if ($n) {
        & docker rm -f $n 2>$null | Out-Null
    }
}

Write-Host "Production stack stopped." -ForegroundColor Green
