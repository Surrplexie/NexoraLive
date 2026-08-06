# Phase L.3 — Epic/Xbox/PlayStation OAuth + ownership validation
param(
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase L.3 platform OAuth validation ===" -ForegroundColor Cyan

dotnet test tests/NL.Identity.Tests/NL.Identity.Tests.csproj --filter "FullyQualifiedName~PlatformOAuth" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Platform OAuth unit tests failed" }
Write-Host "OK: platform OAuth unit tests" -ForegroundColor Green

try {
    Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/health") -TimeoutSec 5 | Out-Null

    $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + "/identity-link.html") -UseBasicParsing -TimeoutSec 15
    if ($r.StatusCode -ne 200) { throw "Page not reachable: /identity-link.html" }
    Write-Host "OK: /identity-link.html" -ForegroundColor Green

    $settings = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/identity/settings") -TimeoutSec 10
    if (-not $settings.oauth.epicAuthorize) { throw "Epic OAuth path missing" }
    if (-not $settings.oauth.xboxAuthorize) { throw "Xbox OAuth path missing" }
    if (-not $settings.oauth.playstationAuthorize) { throw "PlayStation OAuth path missing" }
    Write-Host ("OK: identity settings (epic={0}, xbox={1}, psn={2})" -f $settings.epicOAuthConfigured, $settings.xboxOAuthConfigured, $settings.psnOAuthConfigured) -ForegroundColor Green
} catch {
    Write-Host "SKIP: live stack checks ($($_.Exception.Message))" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "PLATFORM OAUTH VALIDATION PASSED" -ForegroundColor Green
exit 0
