# Phase 5 — stop public beta stack
param([switch]$RemoveVolumes)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "Stopping beta stack..." -ForegroundColor Yellow
$args = @("compose", "-f", "docker/docker-compose.beta-fleet.yml", "down")
if ($RemoveVolumes) { $args += "-v" }
& docker @args

$names = @(docker ps -a --filter "name=nl-fork-" --format "{{.Names}}" 2>$null)
foreach ($n in $names) {
    if ($n) { & docker rm -f $n 2>$null | Out-Null }
}

Write-Host "Beta stack stopped." -ForegroundColor Green
