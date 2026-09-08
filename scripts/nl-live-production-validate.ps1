# Phase 7 — live production validation (HTTPS + live Steam + GA on public-ready config)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$HttpsBaseUrl = "https://127.0.0.1",
    [string]$OperatorKey = "",
    [string]$StreamerId = "dogfood-streamer",
    [string]$NlePath = "/app/samples/configs/fork-hello.nle",
    [switch]$SkipHttpsCheck
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\live-production-fleet.env"
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
    throw "OperatorKey required (pass -OperatorKey or run nl-live-production-stack-up.ps1 -Validate)"
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
        TimeoutSec = 120
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Compress) }
    return Invoke-RestMethod @params
}

Write-Host "=== NL Phase 7 live production validation ===" -ForegroundColor Cyan

Invoke-NlApi GET "/health" | Out-Null

$status = Invoke-NlApi GET "/api/v1/live-production/status"
Write-Host ("Live production: {0}  dev: {1}  identity: {2}  steam: {3}" -f `
    $status.enabled, $status.devMode, $status.identityMode, $status.steamConfigured)
Write-Host ("Public URL: {0}" -f $status.publicBaseUrl) -ForegroundColor DarkGray

$identity = Invoke-NlApi GET "/api/v1/identity/settings"
if ($identity.mode -ne "Live") {
    throw ("Expected identity mode Live, got {0}" -f $identity.mode)
}
Write-Host "OK: live Steam identity mode" -ForegroundColor Green

if (-not $SkipHttpsCheck) {
    Write-Host "Checking edge reverse proxy..." -ForegroundColor Yellow
    Invoke-RestMethod -Uri "http://127.0.0.1/health" -TimeoutSec 15 | Out-Null
    Write-Host "OK: edge proxy reachable on :80" -ForegroundColor Green
}

Write-Host "Checking GA catalog..." -ForegroundColor Yellow
$catalog = Invoke-NlApi GET "/api/v1/ga/catalog"
foreach ($required in @("hello-fork", "minecraft", "beamng")) {
    $found = @($catalog.games | Where-Object { $_.gameId -eq $required })
    if (-not $found) { throw ("Missing catalog game: {0}" -f $required) }
}
Write-Host "OK: multi-game catalog" -ForegroundColor Green

Write-Host "Running dogfood smoke (operator + docker fork + join)..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-dogfood-flow.ps1") -BaseUrl $BaseUrl -StreamerId $StreamerId -ExpectProvisioner docker -SkipImageBuild -OperatorKey $OperatorKey

Write-Host "Running live production validation gate..." -ForegroundColor Yellow
$report = Invoke-NlApi POST "/api/v1/live-production/validation/run" @{} -Operator

Write-Host ""
Write-Host "Live production validation checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.liveProductionPassed) {
    Write-Host "LIVE PRODUCTION VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "LIVE PRODUCTION VALIDATION FAILED" -ForegroundColor Red
exit 1
