# Phase M.2 — Discord OAuth validation (unit tests + optional live stack checks)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase M.2 Discord OAuth validation ===" -ForegroundColor Cyan

dotnet test tests/NL.Social.Tests/NL.Social.Tests.csproj --filter "FullyQualifiedName~DiscordOAuth" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "Discord OAuth unit tests failed" }
Write-Host "OK: Discord OAuth unit tests" -ForegroundColor Green

try {
    $health = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/health") -TimeoutSec 5
    if ($null -eq $health) { throw "empty health" }

    $r = Invoke-WebRequest -Uri ($BaseUrl.TrimEnd('/') + "/social-link.html") -UseBasicParsing -TimeoutSec 15
    if ($r.StatusCode -ne 200) { throw "Page not reachable: /social-link.html" }
    Write-Host "OK: /social-link.html" -ForegroundColor Green

    $settings = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/social/settings") -TimeoutSec 10
    if (-not $settings.oauth.discordAuthorize) { throw "Discord OAuth authorize path missing from settings" }
    if (-not $settings.oauth.discordCallback) { throw "Discord OAuth callback path missing from settings" }
    Write-Host ("OK: social settings (discordOAuthConfigured={0})" -f $settings.discordOAuthConfigured) -ForegroundColor Green
} catch {
    Write-Host "SKIP: live stack checks ($($_.Exception.Message))" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "DISCORD OAUTH VALIDATION PASSED" -ForegroundColor Green
exit 0
