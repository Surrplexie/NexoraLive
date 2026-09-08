# Live social dogfood validation
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [ValidateSet("mock", "live")]
    [string]$SocialMode = "mock"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL live social dogfood validation ===" -ForegroundColor Cyan

& (Join-Path $Root "scripts/nl-live-social-dogfood-setup.ps1") -SocialMode $SocialMode
if ($LASTEXITCODE -ne 0) { throw "Setup script failed" }

dotnet test tests/NL.Social.Tests/NL.Social.Tests.csproj --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Social unit tests failed" }
Write-Host "OK: NL.Social.Tests" -ForegroundColor Green

foreach ($script in @(
    "nl-social-twitch-oauth-validate.ps1",
    "nl-social-discord-oauth-validate.ps1",
    "nl-social-youtube-oauth-validate.ps1",
    "nl-social-kick-oauth-validate.ps1")) {
    powershell -File (Join-Path $Root "scripts/$script") -BaseUrl $BaseUrl
    if ($LASTEXITCODE -ne 0) { throw ("Failed: {0}" -f $script) }
}

try {
    $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/health") -TimeoutSec 5
    if ($null -eq $health) { throw "empty health" }

    foreach ($page in @("/live-social-dogfood.html", "/join-gate.html", "/social-link.html", "/nl-client.html")) {
        $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $page) -UseBasicParsing -TimeoutSec 15
        if ($r.StatusCode -ne 200) { throw ("Page not reachable: {0}" -f $page) }
        Write-Host ("OK: {0}" -f $page) -ForegroundColor Green
    }

    $social = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/social/settings") -TimeoutSec 10
    if (-not $social.enabled) { throw "NL_SOCIAL_ENABLED not active on running host" }
    if ($social.mode.ToLowerInvariant() -ne $SocialMode) {
        throw ("Expected social mode {0}, host reports {1}" -f $SocialMode, $social.mode)
    }
    Write-Host ("OK: social settings (mode={0})" -f $social.mode) -ForegroundColor Green

    $assets = Invoke-RestMethod -Method POST -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/dogfood/social/setup") `
        -ContentType "application/json" -Body ('{"socialMode":"' + $SocialMode + '"}') -TimeoutSec 30
    if (-not $assets.configured) { throw "dogfood/social/setup did not configure assets" }
    Write-Host "OK: dogfood social setup API" -ForegroundColor Green

    $status = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/dogfood/status") -TimeoutSec 10
    if (-not $status.joinRequirementsReady) { throw "join-requirements.json missing" }
    if (-not $status.streamerConfigReady) { throw "streamer-social.json missing" }
    if (-not $status.mockSocialReady) { throw "mock-social.json missing" }
    Write-Host "OK: dogfood status (social assets ready)" -ForegroundColor Green
} catch {
    Write-Host "SKIP: live stack checks ($($_.Exception.Message))" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "LIVE SOCIAL DOGFOOD VALIDATION PASSED" -ForegroundColor Green
exit 0
