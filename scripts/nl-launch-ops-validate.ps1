# Phase 9 — launch ops validation (trust layer + multi-game prerequisite)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [string]$StreamerId = "dogfood-streamer",
    [switch]$SkipMultiGameDogfood
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\launch-ops-fleet.env"
    if (Test-Path $envFile) {
        foreach ($line in Get-Content $envFile) {
            if ($line -match '^NL_OPERATOR_KEY=(.+)$') {
                $OperatorKey = $Matches[1].Trim()
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    throw "OperatorKey required (pass -OperatorKey or run nl-launch-ops-stack-up.ps1 -Validate)"
}

function Invoke-NlApi {
    param([string]$Method, [string]$Path, $Body = $null, [switch]$Operator)
    $uri = ($BaseUrl.TrimEnd('/') + $Path)
    $headers = @{ "Content-Type" = "application/json" }
    if ($Operator) { $headers["X-NL-Operator-Key"] = $OperatorKey }
    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        ErrorAction = "Stop"
        TimeoutSec = 180
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 6 -Compress) }
    return Invoke-RestMethod @params
}

Write-Host "=== NL Phase 9 launch ops validation ===" -ForegroundColor Cyan

Invoke-NlApi GET "/health" | Out-Null

$settings = Invoke-NlApi GET "/api/v1/launch-ops/settings"
if (-not $settings.enabled) {
    throw "NL_LAUNCH_OPS_ENABLED is not true on session-host"
}
Write-Host ("Launch ops enabled; legal version {0}" -f $settings.legal.version) -ForegroundColor DarkGray

Write-Host "Checking public status page..." -ForegroundColor Yellow
$statusPage = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + "/status.html") -UseBasicParsing -TimeoutSec 30
if ($statusPage.StatusCode -ne 200) { throw "status.html not reachable" }
$statusJson = Invoke-NlApi GET "/api/v1/launch-ops/status"
Write-Host ("OK: status API overall={0}" -f $statusJson.overallStatus) -ForegroundColor Green

Write-Host "Checking legal pages..." -ForegroundColor Yellow
foreach ($path in @("/terms.html", "/privacy.html")) {
    $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $path) -UseBasicParsing -TimeoutSec 30
    if ($r.StatusCode -ne 200) { throw ("Legal page not reachable: {0}" -f $path) }
    Write-Host ("OK: {0}" -f $path) -ForegroundColor Green
}

Write-Host "Checking abuse hardening..." -ForegroundColor Yellow
$health = Invoke-NlApi GET "/health"
if (-not $health.hardening) {
    throw "NL_HARDENING is not enabled (health.hardening=false)"
}
Write-Host "OK: hardening enabled" -ForegroundColor Green

Write-Host "Running multi-game validation (prerequisite)..." -ForegroundColor Yellow
$mgArgs = @{
    BaseUrl = $BaseUrl
    OperatorKey = $OperatorKey
    StreamerId = $StreamerId
}
if ($SkipMultiGameDogfood) { $mgArgs.SkipPerGameDogfood = $true }
& (Join-Path $Root "scripts/nl-multi-game-validate.ps1") @mgArgs
if ($LASTEXITCODE -ne 0) { throw "Multi-game validation failed" }

Write-Host "Running fleet backup..." -ForegroundColor Yellow
$backup = Invoke-NlApi POST "/api/v1/launch-ops/backup/run" @{} -Operator
Write-Host ("OK: backup snapshot {0}" -f $backup.snapshotDir) -ForegroundColor Green

$catalog = Invoke-NlApi GET "/api/v1/multigame/catalog"
$verifiedGameIds = @($catalog.games | ForEach-Object { [string]$_.gameId })

Write-Host "Running launch ops validation gate..." -ForegroundColor Yellow
$body = @{
    legalPagesVerified = $true
    hostBackupVerified = $false
    alertingTestPassed = $false
    multiGame = @{
        hostImagesVerified = $true
        verifiedGameIds = $verifiedGameIds
    }
}
$report = Invoke-NlApi POST "/api/v1/launch-ops/validation/run" $body -Operator

Write-Host ""
Write-Host "Launch ops validation checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.launchOpsPassed) {
    Write-Host "LAUNCH OPS VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "LAUNCH OPS VALIDATION FAILED" -ForegroundColor Red
exit 1
