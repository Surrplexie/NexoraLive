# Phase 6 - general availability validation (catalog + open signup + join smoke)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [string]$StreamerId = "dogfood-streamer",
    [string]$NlePath = "/app/samples/configs/fork-hello.nle"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\ga-fleet.env"
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
    throw "OperatorKey required (pass -OperatorKey or run nl-ga-stack-up.ps1 -Validate)"
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
    try {
        return Invoke-RestMethod @params
    } catch {
        throw $_.Exception.Message
    }
}

Write-Host "=== NL Phase 6 general availability validation ===" -ForegroundColor Cyan

Invoke-NlApi GET "/health" | Out-Null

$gaStatus = Invoke-NlApi GET "/api/v1/ga/status"
Write-Host ("GA enabled: {0}  open signup: {1}  catalog games: {2}/{3}  SLA: {4}" -f `
    $gaStatus.enabled, $gaStatus.openSignup, $gaStatus.catalogGameCount, $gaStatus.requiredCatalogGames, $gaStatus.slaTier)

$betaStatus = Invoke-NlApi GET "/api/v1/beta/status"
if ($betaStatus.enabled) {
    throw "Beta program still enabled; GA requires NL_BETA_ENABLED=false"
}
Write-Host "OK: beta disabled" -ForegroundColor Green

Write-Host "Checking multi-game catalog..." -ForegroundColor Yellow
$catalog = Invoke-NlApi GET "/api/v1/ga/catalog"
if (-not $catalog.enabled) { throw "Fork catalog not enabled" }
$gameIds = @($catalog.games | ForEach-Object { $_.gameId })
foreach ($required in @("hello-fork", "minecraft", "beamng")) {
    if ($gameIds -notcontains $required) {
        throw ("Required GA game missing from catalog: {0}" -f $required)
    }
}
Write-Host ("OK: catalog has {0} games ({1})" -f $catalog.games.Count, ($gameIds -join ", ")) -ForegroundColor Green

Write-Host "Testing open streamer registration..." -ForegroundColor Yellow
$contact = ("ga-validate-{0}@nl.test" -f ([guid]::NewGuid().ToString('N').Substring(0, 8)))
$entry = Invoke-NlApi POST "/api/v1/ga/streamers/register" @{
    displayName = "GA Validate"
    contact = $contact
    twitchHandle = "nlgavalidate"
    preferredGameId = "hello-fork"
    streamerId = "ga-open-streamer"
}
Write-Host ("Registered streamer: {0}" -f $entry.streamerId) -ForegroundColor Green

Write-Host "Verifying open fork create (no beta allowlist)..." -ForegroundColor Yellow
$fork = Invoke-NlApi POST "/api/v1/fork/orchestrator/create" @{
    streamerId = "any-open-streamer"
    gameId = "hello-fork"
    majorVersion = "1.0"
    nlePath = $NlePath
    modIds = @()
    twitchFollowers = 100
}
if (-not $fork.sessionId) {
    throw "Expected open fork create to return sessionId"
}
Write-Host ("OK: open streamer fork create allowed (session={0})" -f $fork.sessionId) -ForegroundColor Green

Write-Host "Testing compliance export..." -ForegroundColor Yellow
Invoke-NlApi POST "/api/v1/fleet/compliance/export/ga-validate-player" @{} | Out-Null
Write-Host "OK: GDPR export endpoint works" -ForegroundColor Green

Write-Host "Running approved streamer dogfood smoke..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-dogfood-flow.ps1") -BaseUrl $BaseUrl -StreamerId $StreamerId -ExpectProvisioner docker -SkipImageBuild -OperatorKey $OperatorKey

Write-Host "Running GA validation gate..." -ForegroundColor Yellow
$report = Invoke-NlApi POST "/api/v1/ga/validation/run" @{} -Operator

Write-Host ""
Write-Host "GA validation checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.gaPassed) {
    Write-Host "GA VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "GA VALIDATION FAILED" -ForegroundColor Red
exit 1
