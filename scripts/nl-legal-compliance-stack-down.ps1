# Phase 13 — stop legal & compliance stack
param([switch]$RemoveVolumes)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "Stopping legal & compliance stack..." -ForegroundColor Yellow
$args = @("compose", "-f", "docker/docker-compose.legal-compliance.yml", "down")
if ($RemoveVolumes) { $args += "-v" }
& docker @args

Write-Host "Legal & compliance stack stopped." -ForegroundColor Green
