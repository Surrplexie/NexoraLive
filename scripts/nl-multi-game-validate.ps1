# Phase 8 — multi-game production validation (fork images + per-game dogfood + gate)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [string]$StreamerId = "dogfood-streamer",
    [switch]$SkipLiveProductionGate,
    [switch]$SkipPerGameDogfood
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\multi-game-production-fleet.env"
    if (-not (Test-Path $envFile)) {
        $envFile = Join-Path $Root "docker\live-production-fleet.env"
    }
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
    throw "OperatorKey required (pass -OperatorKey or run nl-multi-game-stack-up.ps1 -Validate)"
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
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Compress) }
    return Invoke-RestMethod @params
}

function Test-DockerImage([string]$Tag) {
    & docker image inspect $Tag 2>$null | Out-Null
    return ($LASTEXITCODE -eq 0)
}

Write-Host "=== NL Phase 8 multi-game production validation ===" -ForegroundColor Cyan

Invoke-NlApi GET "/health" | Out-Null

$settings = Invoke-NlApi GET "/api/v1/multigame/settings"
if (-not $settings.enabled) {
    throw "NL_MULTIGAME_PRODUCTION_ENABLED is not true on session-host"
}
Write-Host ("Multi-game program enabled; required games: {0}" -f ($settings.requiredGameIds -join ", ")) -ForegroundColor DarkGray

$status = Invoke-NlApi GET "/api/v1/multigame/status"
Write-Host ("Live production: {0}  GA: {1}  catalog: {2}  partnership: {3}" -f `
    $status.liveProductionEnabled, $status.gaEnabled, $status.catalogEnabled, $status.partnershipEnabled)

Write-Host "Checking catalog Docker images..." -ForegroundColor Yellow
$catalog = Invoke-NlApi GET "/api/v1/multigame/catalog"
$verifiedGameIds = @()
foreach ($game in $catalog.games) {
    $image = [string]$game.dockerImage
    if ([string]::IsNullOrWhiteSpace($image)) {
        throw ("Catalog missing dockerImage for {0}" -f $game.gameId)
    }
    if (-not (Test-DockerImage $image)) {
        throw ("Docker image not found on host: {0} (game {1}). Run build-fork-images.ps1 -Images all" -f $image, $game.gameId)
    }
    Write-Host ("OK: {0} -> {1}" -f $game.gameId, $image) -ForegroundColor Green
    $verifiedGameIds += [string]$game.gameId
}

if (-not $SkipLiveProductionGate) {
    Write-Host "Running live production validation gate..." -ForegroundColor Yellow
    $liveReport = Invoke-NlApi POST "/api/v1/live-production/validation/run" @{} -Operator
    if (-not $liveReport.liveProductionPassed) {
        Write-Host "Live production gate failed (required for multi-game in production):" -ForegroundColor Red
        foreach ($c in $liveReport.checks) {
            if (-not $c.passed) {
                Write-Host ("  FAIL: {0}" -f $c.description) -ForegroundColor Red
            }
        }
        throw "Live production validation must pass before multi-game gate"
    }
    Write-Host "OK: live production validation passed" -ForegroundColor Green
}

if (-not $SkipPerGameDogfood) {
    foreach ($gameId in $verifiedGameIds) {
        Write-Host ("Running dogfood smoke for {0}..." -f $gameId) -ForegroundColor Yellow
        & (Join-Path $Root "scripts/nl-dogfood-flow.ps1") `
            -BaseUrl $BaseUrl `
            -StreamerId $StreamerId `
            -GameId $gameId `
            -ExpectProvisioner docker `
            -SkipImageBuild `
            -OperatorKey $OperatorKey
        if ($LASTEXITCODE -ne 0) {
            throw ("Dogfood failed for game {0}" -f $gameId)
        }
    }
    Write-Host "OK: per-game fork provision + player join smoke" -ForegroundColor Green
}

Write-Host "Running multi-game validation gate..." -ForegroundColor Yellow
$body = @{
    hostImagesVerified = $true
    verifiedGameIds = $verifiedGameIds
}
$report = Invoke-NlApi POST "/api/v1/multigame/validation/run" $body -Operator

Write-Host ""
Write-Host "Multi-game validation checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.multiGamePassed) {
    Write-Host "MULTIGAME VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "MULTIGAME VALIDATION FAILED" -ForegroundColor Red
exit 1
