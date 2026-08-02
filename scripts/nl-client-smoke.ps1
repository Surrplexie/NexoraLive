# Phase R NL Client smoke
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase R client smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Client/NL.Client.csproj -c Release --verbosity quiet
dotnet test tests/NL.Client.Tests/NL.Client.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase R NL Client smoke OK" -ForegroundColor Green
