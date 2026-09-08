# Dogfood stub for {{GAME_ID}} — wraps shared nl-dogfood-flow.ps1
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [ValidateSet("mock", "process", "docker", "auto")]
    [string]$ExpectProvisioner = "mock",
    [switch]$SkipImageBuild
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$params = @{
    BaseUrl = $BaseUrl
    GameId = "{{GAME_ID}}"
    MajorVersion = "{{MAJOR}}"
    ExpectProvisioner = $ExpectProvisioner
}
if ($OperatorKey) { $params.OperatorKey = $OperatorKey }
if ($SkipImageBuild) { $params.SkipImageBuild = $true }

& (Join-Path $Root "scripts/nl-dogfood-flow.ps1") @params
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Dogfood OK for {{GAME_ID}}" -ForegroundColor Green
