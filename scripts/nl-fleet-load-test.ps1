# Phase S fleet operations smoke
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase S fleet ops smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Fleet/NL.Fleet.csproj -c Release --verbosity quiet
dotnet test tests/NL.Fleet.Tests/NL.Fleet.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase S fleet ops smoke OK" -ForegroundColor Green
