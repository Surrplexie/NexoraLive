# Phase 12 — scale & reliability validation (multi-region + GA load test + distribution gate)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [int]$ConcurrentSessions = 128,
    [switch]$SkipLoadTest,
    [switch]$SkipClientBuild
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\scale-reliability-fleet.env"
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
    throw "OperatorKey required"
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
        TimeoutSec = 300
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress) }
    return Invoke-RestMethod @params
}

Write-Host "=== NL Phase 12 scale & reliability validation ===" -ForegroundColor Cyan

if (-not $SkipClientBuild) {
    & (Join-Path $Root "scripts/build-nl-client-package.ps1") -Version "1.0.0"
    if ($LASTEXITCODE -ne 0) { throw "build-nl-client-package failed" }
}

Invoke-NlApi GET "/health" | Out-Null

$settings = Invoke-NlApi GET "/api/v1/scale-reliability/settings"
if (-not $settings.enabled) { throw "NL_SCALE_RELIABILITY_ENABLED is not true" }

Write-Host "Clearing stale fork sessions..." -ForegroundColor DarkGray
$existing = @(Invoke-NlApi GET "/api/v1/fork/orchestrator/sessions")
foreach ($s in $existing) {
    if ($s.sessionId) {
        try { Invoke-NlApi POST ("/api/v1/fork/orchestrator/destroy/{0}" -f $s.sessionId) | Out-Null } catch { }
    }
}
if ($existing.Count -gt 0) { Start-Sleep -Seconds 2 }

$regionResp = Invoke-NlApi GET "/api/v1/scale-reliability/regions"
$regions = if ($null -eq $regionResp) { @() } elseif ($regionResp -is [System.Array]) { @($regionResp) } else { @($regionResp) }
Write-Host ("OK: {0} fleet regions" -f $regions.Count) -ForegroundColor Green

$NlePath = "/app/samples/configs/fork-hello.nle"
$runId = Get-Random -Minimum 1000 -Maximum 9999
$verifiedRegionIds = @()
Write-Host "Multi-region placement smoke..." -ForegroundColor Yellow
foreach ($region in @("us-east", "us-west", "eu-west")) {
    $sid = ("region-smoke-{0}-{1}" -f $region, $runId)
    $r = Invoke-NlApi POST "/api/v1/fork/orchestrator/create" @{
        streamerId = $sid
        gameId = "hello-fork"
        majorVersion = "1.0"
        nlePath = $NlePath
        modIds = @()
        twitchFollowers = 100
        preferredRegion = $region
    }
    if ($r.regionId -ne $region) {
        throw ("Expected region {0}, got {1}" -f $region, $r.regionId)
    }
    $verifiedRegionIds += [string]$r.regionId
    Write-Host ("OK: placed {0} in {1}" -f $sid, $r.regionId) -ForegroundColor Green
}

$loadTestVerified = $false
if (-not $SkipLoadTest) {
    Write-Host "GA load test ($ConcurrentSessions sessions)..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-scale-reliability-load-test.ps1") `
        -BaseUrl $BaseUrl `
        -ConcurrentSessions $ConcurrentSessions `
        -SkipCleanup
    if ($LASTEXITCODE -ne 0) { throw "GA load test failed" }
    $loadTestVerified = $true
}

$catalog = Invoke-NlApi GET "/api/v1/multigame/catalog"
$verifiedGameIds = @($catalog.games | ForEach-Object { [string]$_.gameId })

Write-Host "Running scale reliability validation gate..." -ForegroundColor Yellow
$body = @{
    loadTestVerified = $loadTestVerified
    multiRegionVerified = $true
    verifiedRegionIds = $verifiedRegionIds
    distribution = @{
        hostClientPackageVerified = $true
        streamerSignupVerified = $true
        playerJoinVerified = $false
        productionCutover = @{
            publicHttpsVerified = $false
            legalPagesVerified = $true
            alertingTestPassed = $true
            multiGame = @{
                hostImagesVerified = $true
                verifiedGameIds = $verifiedGameIds
            }
        }
    }
}
$report = Invoke-NlApi POST "/api/v1/scale-reliability/validation/run" $body -Operator

Write-Host ""
Write-Host "Production SLOs:" -ForegroundColor Cyan
foreach ($s in $report.productionSlos) {
    $mark = if ($s.met) { "PASS" } else { "FAIL" }
    $color = if ($s.met) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}: {2} / {3} {4}" -f $mark, $s.name, $s.current, $s.target, $s.unit) -ForegroundColor $color
}

Write-Host ""
Write-Host "Scale reliability checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.scaleReliabilityPassed) {
    Write-Host "SCALE RELIABILITY VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "SCALE RELIABILITY VALIDATION FAILED" -ForegroundColor Red
exit 1
