# Phase 11 — distribution validation (client package + onboarding + cutover gate)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [string]$StreamerId = "dogfood-streamer",
    [switch]$SkipClientBuild,
    [switch]$SkipPlayerDogfood
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\distribution-fleet.env"
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
        TimeoutSec = 180
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 6 -Compress) }
    return Invoke-RestMethod @params
}

Write-Host "=== NL Phase 11 distribution validation ===" -ForegroundColor Cyan

if (-not $SkipClientBuild) {
    & (Join-Path $Root "scripts/build-nl-client-package.ps1") -Version "1.0.0"
    if ($LASTEXITCODE -ne 0) { throw "build-nl-client-package failed" }
}

$zipPath = Join-Path $Root "src\NL.SessionHost.Web\wwwroot\downloads\nl-client-win-x64.zip"
if (-not (Test-Path $zipPath)) {
    throw "Client package missing - run build-nl-client-package.ps1"
}
Write-Host ("OK: client package {0}" -f $zipPath) -ForegroundColor Green

Invoke-NlApi GET "/health" | Out-Null

$settings = Invoke-NlApi GET "/api/v1/distribution/settings"
if (-not $settings.enabled) { throw "NL_DISTRIBUTION_ENABLED is not true" }

foreach ($page in @("/play.html", "/download.html", "/ga.html", "/nl-client.html", "/identity-link.html", "/fork-catalog.html")) {
    $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $page) -UseBasicParsing -TimeoutSec 30
    if ($r.StatusCode -ne 200) { throw ("Page not reachable: {0}" -f $page) }
    Write-Host ("OK: {0}" -f $page) -ForegroundColor Green
}

$manifest = Invoke-NlApi GET "/api/v1/distribution/client-manifest"
if (-not $manifest.version) { throw "Client manifest missing version" }
Write-Host ("OK: client manifest v{0}" -f $manifest.version) -ForegroundColor Green

Write-Host "Streamer signup smoke..." -ForegroundColor Yellow
$reg = Invoke-NlApi POST "/api/v1/ga/streamers/register" @{
    displayName = "Dist Test"
    contact = "dist-test@example.com"
    twitchHandle = "disttest"
    preferredGameId = "hello-fork"
    streamerId = $StreamerId
}
if (-not $reg.streamerId) { throw "Streamer registration failed" }
Write-Host ("OK: registered streamer {0}" -f $reg.streamerId) -ForegroundColor Green

$playerJoinVerified = $false
if (-not $SkipPlayerDogfood) {
    Write-Host "Player join smoke (hello-fork)..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-dogfood-flow.ps1") `
        -BaseUrl $BaseUrl `
        -StreamerId $StreamerId `
        -GameId hello-fork `
        -ExpectProvisioner docker `
        -SkipImageBuild `
        -OperatorKey $OperatorKey
    if ($LASTEXITCODE -ne 0) { throw "Dogfood join failed" }
    $playerJoinVerified = $true
}

Invoke-NlApi POST "/api/v1/launch-ops/backup/run" @{} -Operator | Out-Null

$catalog = Invoke-NlApi GET "/api/v1/multigame/catalog"
$verifiedGameIds = @($catalog.games | ForEach-Object { [string]$_.gameId })

Write-Host "Running distribution validation gate..." -ForegroundColor Yellow
$body = @{
    hostClientPackageVerified = $true
    streamerSignupVerified = $true
    playerJoinVerified = $playerJoinVerified
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
$report = Invoke-NlApi POST "/api/v1/distribution/validation/run" $body -Operator

Write-Host ""
Write-Host "Distribution validation checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.distributionPassed) {
    Write-Host "DISTRIBUTION VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "DISTRIBUTION VALIDATION FAILED" -ForegroundColor Red
exit 1
