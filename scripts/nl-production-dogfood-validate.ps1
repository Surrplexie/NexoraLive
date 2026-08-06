# Production dogfood — full E2E validation on Docker fork images
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [string]$StreamerId = "dogfood-streamer",
    [string]$PlayerId = "sp-dogfood-1",
    [string]$Steam64 = "76561198000000001",
    [switch]$SkipClientBuild,
    [switch]$AllGames
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\production-dogfood-fleet.env"
    if (Test-Path $envFile) {
        foreach ($line in Get-Content $envFile) {
            if ($line -match '^NL_OPERATOR_KEY=(.+)$') {
                $OperatorKey = $Matches[1].Trim()
                break
            }
        }
    }
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
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress) }
    return Invoke-RestMethod @params
}

Write-Host "=== NL production dogfood validation ===" -ForegroundColor Cyan

if (-not $SkipClientBuild) {
    & (Join-Path $Root "scripts/build-nl-client-package.ps1") -Version "1.0.0"
    if ($LASTEXITCODE -ne 0) { throw "build-nl-client-package failed" }
}

Invoke-NlApi GET "/health" | Out-Null

$settings = Invoke-NlApi GET "/api/v1/production-dogfood/settings"
if (-not $settings.enabled) { throw "NL_PRODUCTION_DOGFOOD_ENABLED is not true" }

$orch = Invoke-NlApi GET "/api/v1/fork/orchestrator/settings"
if ($orch.mode -ne "Docker") {
    throw ("Expected Docker fork provisioner, got {0}" -f $orch.mode)
}
Write-Host ("OK: orchestrator mode={0}" -f $orch.mode) -ForegroundColor Green

foreach ($page in @(
    "/play.html", "/ga.html", "/identity-link.html", "/social-link.html",
    "/nl-client.html", "/production-dogfood-ops.html")) {
    $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $page) -UseBasicParsing -TimeoutSec 30
    if ($r.StatusCode -ne 200) { throw ("Page not reachable: {0}" -f $page) }
    Write-Host ("OK: {0}" -f $page) -ForegroundColor Green
}

Write-Host "Streamer GA signup..." -ForegroundColor Yellow
$reg = Invoke-NlApi POST "/api/v1/ga/streamers/register" @{
    displayName = "Production Dogfood"
    contact = "dogfood@example.com"
    twitchHandle = "dogfood"
    preferredGameId = "hello-fork"
    streamerId = $StreamerId
    termsAccepted = $true
}
if (-not $reg.streamerId) { throw "Streamer registration failed" }
Write-Host ("OK: streamer {0}" -f $reg.streamerId) -ForegroundColor Green

Write-Host "NL identity account + Steam link..." -ForegroundColor Yellow
$acct = Invoke-NlApi POST "/api/v1/identity/accounts" @{ displayName = "Dogfood Player" }
if (-not $acct.accountId) { throw "Identity account create failed" }
$link = Invoke-NlApi POST "/api/v1/identity/link" @{
    accountId = $acct.accountId
    platform = "steam"
    externalUserId = $Steam64
}
if (-not $link.accountId) { throw "Identity Steam link failed" }
Write-Host ("OK: identity account {0}" -f $acct.accountId) -ForegroundColor Green

$playerJoinVerified = $false
$minecraftJoinVerified = $false
$beamngJoinVerified = $false
$forkTeardownVerified = $false

Write-Host "hello-fork Docker dogfood..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-dogfood-flow.ps1") `
    -BaseUrl $BaseUrl `
    -StreamerId $StreamerId `
    -PlayerId $PlayerId `
    -Steam64 $Steam64 `
    -GameId hello-fork `
    -ExpectProvisioner docker `
    -SkipImageBuild `
    -OperatorKey $OperatorKey `
    -NlAccountId $acct.accountId
if ($LASTEXITCODE -ne 0) { throw "hello-fork dogfood failed" }
$playerJoinVerified = $true
$forkTeardownVerified = $true
Write-Host "OK: hello-fork join + teardown" -ForegroundColor Green

if ($AllGames) {
    Write-Host "minecraft Docker dogfood..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-dogfood-flow.ps1") `
        -BaseUrl $BaseUrl `
        -StreamerId $StreamerId `
        -PlayerId ($PlayerId + "-mc") `
        -Steam64 $Steam64 `
        -GameId minecraft `
        -ExpectProvisioner docker `
        -SkipImageBuild `
        -OperatorKey $OperatorKey `
        -NlAccountId $acct.accountId `
        -VerifyRuleEvents
    if ($LASTEXITCODE -ne 0) { throw "minecraft dogfood failed" }
    $minecraftJoinVerified = $true
    Write-Host "OK: minecraft join + teardown" -ForegroundColor Green

    Write-Host "beamng Docker dogfood..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-dogfood-flow.ps1") `
        -BaseUrl $BaseUrl `
        -StreamerId $StreamerId `
        -PlayerId ($PlayerId + "-bg") `
        -Steam64 $Steam64 `
        -GameId beamng `
        -ExpectProvisioner docker `
        -SkipImageBuild `
        -OperatorKey $OperatorKey `
        -NlAccountId $acct.accountId
    if ($LASTEXITCODE -ne 0) { throw "beamng dogfood failed" }
    $beamngJoinVerified = $true
    Write-Host "OK: beamng join + teardown" -ForegroundColor Green
}

Write-Host "Running production dogfood validation gate..." -ForegroundColor Yellow
$body = @{
    streamerSignupVerified = $true
    identityAccountVerified = $true
    playerJoinVerified = $playerJoinVerified
    minecraftJoinVerified = $minecraftJoinVerified
    beamngJoinVerified = $beamngJoinVerified
    forkTeardownVerified = $forkTeardownVerified
    streamerId = $StreamerId
    verifiedGames = @("hello-fork")
}
if ($AllGames) {
    $body.verifiedGames = @("hello-fork", "minecraft", "beamng")
}
$report = Invoke-NlApi POST "/api/v1/production-dogfood/validation/run" $body -Operator

Write-Host ""
Write-Host "Production dogfood checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.productionDogfoodPassed) {
    Write-Host "PRODUCTION DOGFOOD VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "PRODUCTION DOGFOOD VALIDATION FAILED" -ForegroundColor Red
exit 1
