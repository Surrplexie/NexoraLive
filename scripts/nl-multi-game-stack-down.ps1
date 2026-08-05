# Phase 8 — stop multi-game production stack
param([switch]$RemoveVolumes)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "Stopping multi-game production stack..." -ForegroundColor Yellow
$args = @("compose", "-f", "docker/docker-compose.multi-game-production.yml", "down")
if ($RemoveVolumes) { $args += "-v" }
& docker @args

$names = @(docker ps -a --filter "name=nl-fork-" --format "{{.Names}}" 2>$null)
foreach ($n in $names) {
    if ($n) { & docker rm -f $n 2>$null | Out-Null }
}

Write-Host "Multi-game production stack stopped." -ForegroundColor Green
