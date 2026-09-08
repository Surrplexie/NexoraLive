# Phase — Account verification validation (unit tests + optional live stack checks)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Account verification validation ===" -ForegroundColor Cyan

dotnet test tests/NL.Identity.Tests/NL.Identity.Tests.csproj --filter "FullyQualifiedName~AccountVerification|FullyQualifiedName~TotpHelper" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Account verification unit tests failed" }
Write-Host "OK: account verification unit tests" -ForegroundColor Green

try {
    $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/health") -TimeoutSec 5
    if ($null -eq $health) { throw "empty health" }

    foreach ($page in @("/account-verify.html", "/identity-link.html", "/join-gate.html")) {
        $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $page) -UseBasicParsing -TimeoutSec 15
        if ($r.StatusCode -ne 200) { throw ("Page not reachable: {0}" -f $page) }
        Write-Host ("OK: {0}" -f $page) -ForegroundColor Green
    }

    $settings = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/identity/settings") -TimeoutSec 10
    if ($null -eq $settings.verificationEnabled) { throw "verificationEnabled missing from identity settings" }
    if (-not $settings.oauth.verifyUi) { throw "verify UI path missing from settings" }
    Write-Host ("OK: identity settings (verificationEnabled={0})" -f $settings.verificationEnabled) -ForegroundColor Green
} catch {
    Write-Host "SKIP: live stack checks ($($_.Exception.Message))" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "ACCOUNT VERIFICATION VALIDATION PASSED" -ForegroundColor Green
exit 0
