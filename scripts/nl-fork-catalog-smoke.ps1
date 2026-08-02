# Phase N fork catalog smoke
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase N fork catalog smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Fork.Catalog/NL.Fork.Catalog.csproj -c Release --verbosity quiet
dotnet test tests/NL.Fork.Catalog.Tests/NL.Fork.Catalog.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase N fork catalog smoke OK" -ForegroundColor Green
