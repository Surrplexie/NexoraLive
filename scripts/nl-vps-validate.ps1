# Remote VPS validation (run from Windows after deploy)
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,
    [string]$OperatorKey = "",
    [switch]$SkipDogfood,
    [switch]$SkipGaLaunch
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$base = $BaseUrl.TrimEnd('/')
$https = $base -replace '^http://', 'https://'

Write-Host "=== NL VPS remote validation ===" -ForegroundColor Cyan
Write-Host ("Target: {0}" -f $https)

Write-Host "DNS + TLS health..." -ForegroundColor Yellow
try {
    Invoke-RestMethod -Uri ($https + "/health") -TimeoutSec 30 | Out-Null
} catch {
    throw ("Health check failed: {0}/health — fix DNS/TLS first." -f $https)
}
Write-Host "OK: /health" -ForegroundColor Green

foreach ($page in @("/play.html", "/ga.html", "/status.html", "/download.html")) {
    $r = Invoke-WebRequest -Uri ($https + $page) -UseBasicParsing -TimeoutSec 30
    if ($r.StatusCode -ne 200) { throw ("Page failed: {0}" -f $page) }
    Write-Host ("OK: {0}" -f $page) -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    Write-Host "WARN: No -OperatorKey — skipping gated validations" -ForegroundColor Yellow
    Write-Host "VPS SMOKE PASSED (health + pages only)" -ForegroundColor Green
    exit 0
}

if (-not $SkipDogfood) {
    Write-Host "Production dogfood (hello-fork)..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-production-dogfood-validate.ps1") `
        -BaseUrl $https `
        -OperatorKey $OperatorKey `
        -SkipClientBuild
    if ($LASTEXITCODE -ne 0) { throw "Production dogfood failed on VPS" }
}

if (-not $SkipGaLaunch) {
    Write-Host "Public GA launch gate..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-public-ga-launch-validate.ps1") `
        -BaseUrl $https `
        -OperatorKey $OperatorKey `
        -SkipClientBuild `
        -SkipLegalPrerequisite
    if ($LASTEXITCODE -ne 0) { throw "Public GA launch validation failed on VPS" }
}

Write-Host ""
Write-Host "VPS PRODUCTION VALIDATION PASSED" -ForegroundColor Green
exit 0
