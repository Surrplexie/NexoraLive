# Dogfood stub for Kenshi (Phase T1)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [ValidateSet("mock", "process", "docker", "auto")]
    [string]$ExpectProvisioner = "mock",
    [switch]$SkipImageBuild,
    [switch]$VerifyRuleEvents
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$params = @{
    BaseUrl = $BaseUrl
    GameId = "kenshi"
    MajorVersion = "1.0"
    ExpectProvisioner = $ExpectProvisioner
}
if ($OperatorKey) { $params.OperatorKey = $OperatorKey }
if ($SkipImageBuild) { $params.SkipImageBuild = $true }
if ($VerifyRuleEvents -or $ExpectProvisioner -eq "docker") { $params.VerifyRuleEvents = $true }

& (Join-Path $Root "scripts/nl-dogfood-flow.ps1") @params
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Dogfood OK for kenshi" -ForegroundColor Green
