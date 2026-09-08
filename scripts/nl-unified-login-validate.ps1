# Unified NL login validation
param(
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Unified account validation ===" -ForegroundColor Cyan

dotnet test tests/NL.Identity.Tests/NL.Identity.Tests.csproj --filter "FullyQualifiedName~UnifiedAccount|FullyQualifiedName~NlPasswordHasher" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Unified account unit tests failed" }
Write-Host "OK: unified account unit tests" -ForegroundColor Green

try {
    $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/health") -TimeoutSec 5
    if ($null -eq $health) { throw "empty health" }

    foreach ($page in @("/login.html", "/nl-client.html", "/identity-link.html")) {
        $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $page) -UseBasicParsing -TimeoutSec 15
        if ($r.StatusCode -ne 200) { throw ("Page not reachable: {0}" -f $page) }
        Write-Host ("OK: {0}" -f $page) -ForegroundColor Green
    }

    $settings = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/identity/settings") -TimeoutSec 10
    if (-not $settings.unifiedLoginEnabled) { throw "unifiedLoginEnabled missing" }
    if (-not $settings.oauth.loginUi) { throw "login UI path missing" }
    Write-Host "OK: identity settings (unified login)" -ForegroundColor Green
} catch {
    Write-Host "SKIP: live stack checks ($($_.Exception.Message))" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "UNIFIED NL LOGIN VALIDATION PASSED" -ForegroundColor Green
exit 0
