# Docker-mode dogfood — hello-fork image (Phase 1.1).
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [switch]$SkipImageBuild
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$params = @{
    ExpectProvisioner = "docker"
    GameId            = "hello-fork"
}
if ($SkipImageBuild) { $params.SkipImageBuild = $true }
if ($BaseUrl) { $params.BaseUrl = $BaseUrl }

& (Join-Path $scriptDir "nl-dogfood-flow.ps1") @params
