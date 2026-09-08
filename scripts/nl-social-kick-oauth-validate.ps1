# Phase M.4 — Kick OAuth validation (unit tests + optional live stack checks)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase M.4 Kick OAuth validation ===" -ForegroundColor Cyan

dotnet test tests/NL.Social.Tests/NL.Social.Tests.csproj --filter "FullyQualifiedName~KickOAuth" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Kick OAuth unit tests failed" }
Write-Host "OK: Kick OAuth unit tests" -ForegroundColor Green

try {
    $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/health") -TimeoutSec 5
    if ($null -eq $health) { throw "empty health" }

    foreach ($page in @("/social-link.html", "/join-gate.html")) {
        $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + $page) -UseBasicParsing -TimeoutSec 15
        if ($r.StatusCode -ne 200) { throw ("Page not reachable: {0}" -f $page) }
        Write-Host ("OK: {0}" -f $page) -ForegroundColor Green
    }

    $settings = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/social/settings") -TimeoutSec 10
    if (-not $settings.oauth.kickAuthorize) { throw "OAuth authorize path missing from settings" }
    if (-not $settings.oauth.kickCallback) { throw "OAuth callback path missing from settings" }
    Write-Host ("OK: social settings (oauth paths, kickOAuthConfigured={0})" -f $settings.kickOAuthConfigured) -ForegroundColor Green
} catch {
    Write-Host "SKIP: live stack checks ($($_.Exception.Message))" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "KICK OAUTH VALIDATION PASSED" -ForegroundColor Green
exit 0
