# T2-lite: public RimWorld connect URI wiring (no live VPS required).
param(
    [string]$BaseUrl = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$example = Join-Path $Root "samples\fleet\vps-production.env.example"
$exampleText = Get-Content $example -Raw
if ($exampleText -notmatch "NL_FORK_PUBLIC_CONNECT_HOST=") {
    throw "vps-production.env.example missing NL_FORK_PUBLIC_CONNECT_HOST"
}

$compose = Get-Content (Join-Path $Root "docker\docker-compose.vps-production.yml") -Raw
if ($compose -notmatch "25555") {
    throw "compose file should document RimWorld 25555"
}

dotnet test tests/NL.Fork.Core.Tests --filter "FullyQualifiedName~PublicForkConnect" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "PublicForkConnect tests failed" }
dotnet test tests/NL.Fleet.Tests --filter "FullyQualifiedName~RelayMasking_KeepsRimWorld" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Fleet RimWorld mask tests failed" }
dotnet test tests/NL.Client.Tests --filter "FullyQualifiedName~LaunchBuilder_RimWorld" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Client clipboard tests failed" }

if ($BaseUrl) {
    $status = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/t2-lite/status")
    if (-not $status.ready) {
        throw "t2-lite/status ready=false on $BaseUrl"
    }
    Write-Host ("Live: {0}" -f $status.rimworldExample) -ForegroundColor Green
}

Write-Host "T2-LITE VALIDATE PASSED" -ForegroundColor Green
