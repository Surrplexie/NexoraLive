# Phase M social gate smoke
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase M social gate smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Social/NL.Social.csproj -c Release --verbosity quiet
dotnet test tests/NL.Social.Tests/NL.Social.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

powershell -File (Join-Path $Root "scripts/nl-social-twitch-oauth-validate.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase M social gate smoke OK" -ForegroundColor Green
