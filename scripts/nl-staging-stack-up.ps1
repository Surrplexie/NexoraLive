# Phase 3 — start hosted staging stack (docker compose)
param(
    [switch]$SkipBuild,
    [switch]$Validate,
    [int]$ConcurrentSessions = 100
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

function Test-PortInUse([int]$Port) {
    try {
        return (Test-NetConnection -ComputerName 127.0.0.1 -Port $Port -WarningAction SilentlyContinue).TcpTestSucceeded
    } catch {
        return $false
    }
}

Write-Host "=== NL Phase 3 staging stack ===" -ForegroundColor Cyan

foreach ($port in @(27020, 443, 8443)) {
    if (Test-PortInUse $port) {
        Write-Host ("Port {0} is already in use." -f $port) -ForegroundColor Yellow
        Write-Host "Stop local NL.SessionHost.Web or an old compose stack first:" -ForegroundColor Yellow
        Write-Host "  powershell -File scripts/nl-staging-stack-down.ps1" -ForegroundColor DarkGray
        Write-Host "  # or Ctrl+C the dotnet run terminal" -ForegroundColor DarkGray
        throw ("Port {0} busy - cannot start staging stack." -f $port)
    }
}

$composeArgs = @("compose", "-f", "docker/docker-compose.staging-fleet.yml", "up")
if (-not $SkipBuild) { $composeArgs += "--build" }
$composeArgs += @("-d")

Write-Host "Starting docker compose staging fleet..." -ForegroundColor Yellow
& docker @composeArgs
if ($LASTEXITCODE -ne 0) {
    throw "docker compose up failed"
}

function Wait-HttpOk([string]$Url, [int]$TimeoutSec = 180) {
    Write-Host ("Waiting for {0} ..." -f $Url) -ForegroundColor DarkGray
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-RestMethod -Uri $Url -TimeoutSec 5 | Out-Null
            return $true
        } catch {
            Start-Sleep -Seconds 3
        }
    }
    return $false
}

if (-not (Wait-HttpOk "http://127.0.0.1:27020/health")) {
    Write-Host "session-host logs:" -ForegroundColor Red
    & docker compose -f docker/docker-compose.staging-fleet.yml logs session-host --tail 40
    throw "session-host /health did not become ready"
}

Write-Host "Session host:  http://127.0.0.1:27020" -ForegroundColor Green
Write-Host "HTTPS edge:    https://127.0.0.1/health  (trust Caddy internal CA)" -ForegroundColor Green
Write-Host "Relay stub:    wss://127.0.0.1:8443/fork/{sessionId}" -ForegroundColor Green
Write-Host "TURN:          turn:127.0.0.1:3478" -ForegroundColor Green
Write-Host "Operator UI:   http://127.0.0.1:27020/operator.html" -ForegroundColor Green

if ($Validate) {
    Write-Host ""
    & (Join-Path $Root "scripts/nl-staging-validate.ps1") -ConcurrentSessions $ConcurrentSessions
}
