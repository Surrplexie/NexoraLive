# Phase 3 — stop hosted staging stack
param([switch]$RemoveVolumes)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "Stopping staging stack..." -ForegroundColor Yellow
$args = @("compose", "-f", "docker/docker-compose.staging-fleet.yml", "down")
if ($RemoveVolumes) { $args += "-v" }
& docker @args
Write-Host "Staging stack stopped." -ForegroundColor Green
