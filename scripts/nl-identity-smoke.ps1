# Phase L identity smoke
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase L identity smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Identity/NL.Identity.csproj -c Release --verbosity quiet
dotnet test tests/NL.Identity.Tests/NL.Identity.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase L identity smoke OK" -ForegroundColor Green
