# Phase Q partnership smoke
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase Q partnership smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Partnership/NL.Partnership.csproj -c Release --verbosity quiet
dotnet test tests/NL.Partnership.Tests/NL.Partnership.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase Q partnership smoke OK" -ForegroundColor Green
