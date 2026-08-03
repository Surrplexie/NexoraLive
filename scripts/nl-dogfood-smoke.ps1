# Dogfood flow smoke (setup API + optional live E2E)
param(
    [switch]$Live,
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL dogfood smoke ===" -ForegroundColor Cyan

dotnet build src/NL.SessionHost.Web/NL.SessionHost.Web.csproj -c Release --verbosity quiet
dotnet test tests/NL.Client.Tests/NL.Client.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Live) {
    & "$Root\scripts\nl-dogfood-flow.ps1" -BaseUrl $BaseUrl
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Dogfood smoke OK" -ForegroundColor Green
