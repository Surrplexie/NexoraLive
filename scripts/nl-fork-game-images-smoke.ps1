# Phase P — real game fork images smoke (Minecraft + BeamNG sidecars)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL Phase P game fork images smoke ===" -ForegroundColor Cyan

dotnet build src/NL.Fork.Core/NL.Fork.Core.csproj -c Release --verbosity quiet
dotnet build src/NL.Fork.Runtime/NL.Fork.Runtime.csproj -c Release --verbosity quiet
dotnet test tests/NL.Fork.Core.Tests/NL.Fork.Core.Tests.csproj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Embedded minecraft profile:" -ForegroundColor DarkGray
dotnet run --project src/NL.Fork.Runtime --no-build -c Release -- --config samples/configs/minecraft.nle --game minecraft
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Embedded beamng profile:" -ForegroundColor DarkGray
dotnet run --project src/NL.Fork.Runtime --no-build -c Release -- --config samples/configs/beamng.nle --game beamng
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase P game fork images smoke OK" -ForegroundColor Green
Write-Host "Docker builds (optional):" -ForegroundColor DarkGray
Write-Host "  docker build -f docker/fork-minecraft/Dockerfile -t nl-fork-minecraft:latest ."
Write-Host "  docker build -f docker/fork-beamng/Dockerfile -t nl-fork-beamng:latest ."
Write-Host "  docker build -f docker/fork-minecraft-paper/Dockerfile -t nl-fork-minecraft-paper:latest ."
