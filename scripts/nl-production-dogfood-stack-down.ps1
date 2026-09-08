# Production dogfood — stop stack
param([switch]$RemoveVolumes)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "Stopping production dogfood stack..." -ForegroundColor Yellow
$args = @("compose", "-f", "docker/docker-compose.production-dogfood.yml", "down")
if ($RemoveVolumes) { $args += "-v" }
& docker @args

$names = @(docker ps -a --filter "name=nl-fork-" --format "{{.Names}}" 2>$null)
foreach ($n in $names) {
    if ($n) { & docker rm -f $n 2>$null | Out-Null }
}

Write-Host "Production dogfood stack stopped." -ForegroundColor Green
