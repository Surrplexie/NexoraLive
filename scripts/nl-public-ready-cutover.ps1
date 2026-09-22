# NL Path A - public ready cutover against a live play host.
#
# Full gate needs a recorded fleet load test (~100 mock forks) so live-production
# production_ready is true, then legal + public GA validation.
#
# CMD:
#   powershell -NoProfile -File scripts\nl-public-ready-cutover.ps1 -BaseUrl "https://play.20062006.xyz" -PagesOnly
#   powershell -NoProfile -File scripts\nl-public-ready-cutover.ps1 -BaseUrl "https://play.20062006.xyz" -OperatorKey "KEY"
param(
    [string]$BaseUrl = "https://play.20062006.xyz",
    [string]$OperatorKey = "",
    [switch]$PagesOnly,
    [switch]$SkipScaleLoadTest,
    [switch]$SkipLegalPrerequisite,
    [switch]$SkipClientBuild,
    [int]$ConcurrentSessions = 100
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = "https://play.20062006.xyz"
}
$BaseUrl = $BaseUrl.Trim().TrimEnd('/')
if ($BaseUrl -notmatch '^https?://[^/]+') {
    throw "Invalid -BaseUrl '$BaseUrl'. Example: -BaseUrl `"https://play.20062006.xyz`""
}

function Invoke-NlApi {
    param([string]$Method, [string]$Path, $Body = $null, [switch]$Operator)
    $uri = $BaseUrl + $Path
    $headers = @{ "Content-Type" = "application/json" }
    if ($Operator) {
        if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
            throw "OperatorKey required for $Path"
        }
        $headers["X-NL-Operator-Key"] = $OperatorKey
    }
    $params = @{
        Uri         = $uri
        Method      = $Method
        Headers     = $headers
        ErrorAction = "Stop"
        TimeoutSec  = 600
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress)
    }
    return Invoke-RestMethod @params
}

Write-Host "=== NL public ready cutover ===" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl"

$health = Invoke-NlApi GET "/health"
if ($health.status -ne "ok") { throw "health not ok" }
if ($health.publicMode -ne $true) { throw "publicMode expected true" }
Write-Host "OK: health publicMode=$($health.publicMode) hardening=$($health.hardening)" -ForegroundColor Green

$identity = Invoke-NlApi GET "/api/v1/identity/settings"
if ($identity.mode -ne "Live") { throw "identity mode=$($identity.mode) (want Live)" }
if ($identity.steamConfigured -ne $true) { throw "steamConfigured false - set STEAM_WEB_API_KEY" }
Write-Host "OK: identity Live steamConfigured=true" -ForegroundColor Green

$t2 = Invoke-NlApi GET "/api/v1/t2-lite/status"
if ($t2.ready -ne $true) { throw "t2-lite not ready" }
Write-Host "OK: t2-lite $($t2.rimworldExample)" -ForegroundColor Green

$ga = Invoke-NlApi GET "/api/v1/public-ga-launch/settings"
if ($ga.enabled -ne $true) { throw "NL_PUBLIC_GA_LAUNCH_ENABLED false" }
if ($ga.devMode -eq $true) { throw "NL_PUBLIC_GA_LAUNCH_DEV must be false on VPS" }
Write-Host "OK: public GA v$($ga.launchVersion) support=$($ga.supportContact) devMode=false" -ForegroundColor Green

$legal = Invoke-NlApi GET "/api/v1/legal-compliance/status"
Write-Host "OK: legal enabled=$($legal.enabled) docs=$($legal.documentCount)" -ForegroundColor Green

foreach ($page in @(
        "/play.html", "/download.html", "/status.html", "/ga-launch-checklist.html",
        "/legal-center.html", "/ga.html", "/nl-client.html", "/public-ga-launch-ops.html",
        "/operator.html", "/identity-link.html", "/terms.html", "/privacy.html")) {
    $r = Invoke-WebRequest -Uri ($BaseUrl + $page) -UseBasicParsing -TimeoutSec 30
    if ($r.StatusCode -ne 200) { throw "Page not reachable: $page" }
    Write-Host "OK: $page" -ForegroundColor Green
}

if ($PagesOnly) {
    Write-Host "PUBLIC READY PAGES OK (pass -OperatorKey for full cutover)" -ForegroundColor Yellow
    exit 0
}

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    throw "OperatorKey required (or use -PagesOnly). Read from VPS docker/vps-production-fleet.env - do not paste into chat."
}

Write-Host "Fleet backup..." -ForegroundColor Yellow
Invoke-NlApi POST "/api/v1/launch-ops/backup/run" @{} -Operator | Out-Null
Write-Host "OK: backup" -ForegroundColor Green

Write-Host "Operator GA signoff..." -ForegroundColor Yellow
Invoke-NlApi POST "/api/v1/public-ga-launch/signoff" @{} -Operator | Out-Null
Write-Host "OK: signoff" -ForegroundColor Green

if (-not $SkipScaleLoadTest) {
    Write-Host ""
    Write-Host "Scale reliability + fleet load test ($ConcurrentSessions mock forks)..." -ForegroundColor Yellow
    Write-Host "This can take several minutes and uses VPS RAM. Leave forks up (SkipCleanup) so production_ready sticks." -ForegroundColor DarkGray
    & (Join-Path $Root "scripts/nl-scale-reliability-validate.ps1") `
        -BaseUrl $BaseUrl `
        -OperatorKey $OperatorKey `
        -ConcurrentSessions $ConcurrentSessions `
        -SkipClientBuild
    if ($LASTEXITCODE -ne 0) {
        throw "Scale reliability failed. On 8GB VPS try -ConcurrentSessions 100. Fix fork create errors, then re-run."
    }
    Write-Host "OK: scale reliability" -ForegroundColor Green

    $live = Invoke-NlApi GET "/api/v1/live-production/validation"
    if ($live.liveProductionPassed -ne $true) {
        Write-Host "WARN: live-production still not passed after load test. Checks:" -ForegroundColor Yellow
        foreach ($c in $live.checks) {
            if (-not $c.passed) {
                Write-Host ("  FAIL {0}: {1}" -f $c.id, $c.detail) -ForegroundColor Red
            }
        }
    }
    else {
        Write-Host "OK: live-production gate" -ForegroundColor Green
    }
}
else {
    Write-Host "SkipScaleLoadTest set - legal may fail if production_ready was never recorded." -ForegroundColor Yellow
}

if (-not $SkipLegalPrerequisite) {
    Write-Host "Legal compliance prerequisite..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-legal-compliance-validate.ps1") `
        -BaseUrl $BaseUrl `
        -OperatorKey $OperatorKey `
        -SkipClientBuild `
        -SkipScaleLoadTest
    if ($LASTEXITCODE -ne 0) {
        $legalVal = Invoke-NlApi GET "/api/v1/legal-compliance/validation"
        Write-Host "Legal checks:" -ForegroundColor Cyan
        foreach ($c in $legalVal.checks) {
            $mark = if ($c.passed) { "PASS" } else { "FAIL" }
            $color = if ($c.passed) { "Green" } else { "Red" }
            Write-Host ("  [{0}] {1} {2}" -f $mark, $c.description, $c.detail) -ForegroundColor $color
        }
        throw "Legal compliance prerequisite failed (see FAIL rows above). Usually missing scale load test / production_ready."
    }
    Write-Host "OK: legal compliance" -ForegroundColor Green
}

$validate = Join-Path $Root "scripts/nl-public-ga-launch-validate.ps1"
& $validate `
    -BaseUrl $BaseUrl `
    -OperatorKey $OperatorKey `
    -SkipClientBuild `
    -SkipLegalPrerequisite
if ($LASTEXITCODE -ne 0) { throw "public GA launch validation failed" }

Write-Host "PUBLIC READY (VPS)" -ForegroundColor Green
Write-Host "Next: log PATH_A_SESSION_LOG / daily-log; destroy leftover nl-fork-* if RAM is tight; Cities stay frozen."
