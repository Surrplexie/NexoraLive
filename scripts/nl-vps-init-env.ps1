# Generate docker/.env.vps + docker/vps-production-fleet.env on Windows, then scp to the VPS.
param(
    [Parameter(Mandatory = $true)]
    [string]$PlayDomain,
    [Parameter(Mandatory = $true)]
    [string]$BaseDomain,
    [Parameter(Mandatory = $true)]
    [string]$AcmeEmail,
    [Parameter(Mandatory = $true)]
    [string]$SupportEmail,
    [string]$SteamWebApiKey = "",
    [string]$WorkspaceHostRoot = "/var/lib/nl/vps-fork-workspace"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

function New-RandomHex([int]$Bytes = 16) {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $buf = New-Object byte[] $Bytes
    $rng.GetBytes($buf)
    return ([BitConverter]::ToString($buf) -replace '-', '').ToLowerInvariant()
}

$operatorKey = New-RandomHex 16
$busToken = New-RandomHex 16
$turnSecret = New-RandomHex 16

$caddySrc = Join-Path $Root "docker\.env.vps.example"
$fleetSrc = Join-Path $Root "samples\fleet\vps-production.env.example"
$caddyDst = Join-Path $Root "docker\.env.vps"
$fleetDst = Join-Path $Root "docker\vps-production-fleet.env"

$caddy = (Get-Content $caddySrc -Raw) `
    -replace "play\.yourdomain\.com", $PlayDomain `
    -replace "yourdomain\.com", $BaseDomain `
    -replace "you@yourdomain\.com", $AcmeEmail `
    -replace "change-me-turn-secret", $turnSecret `
    -replace "/var/lib/nl/vps-fork-workspace", $WorkspaceHostRoot
Set-Content -Path $caddyDst -Value $caddy -Encoding ASCII

$fleet = (Get-Content $fleetSrc -Raw) `
    -replace "play\.yourdomain\.com", $PlayDomain `
    -replace "yourdomain\.com", $BaseDomain `
    -replace "support@yourdomain\.com", $SupportEmail `
    -replace "NL_OPERATOR_KEY=change-me", "NL_OPERATOR_KEY=$operatorKey" `
    -replace "NL_BUS_TOKEN=change-me", "NL_BUS_TOKEN=$busToken" `
    -replace "change-me-turn-secret", $turnSecret `
    -replace "STEAM_WEB_API_KEY=", "STEAM_WEB_API_KEY=$SteamWebApiKey" `
    -replace "/var/lib/nl/vps-fork-workspace", $WorkspaceHostRoot
Set-Content -Path $fleetDst -Value $fleet -Encoding ASCII

Write-Host "Wrote docker\.env.vps and docker\vps-production-fleet.env" -ForegroundColor Green
Write-Host ("Operator key (save offline): {0}" -f $operatorKey) -ForegroundColor Yellow
Write-Host "DNS A records -> VPS IP:"
Write-Host ("  {0}" -f $PlayDomain)
Write-Host "  Firewall: 25555/tcp (RimWorld T2-lite)"
Write-Host ("  relay-us-east.{0}" -f $BaseDomain)
Write-Host ("  relay-us-west.{0}" -f $BaseDomain)
Write-Host ("  relay-eu-west.{0}" -f $BaseDomain)
Write-Host "Next: powershell -File scripts/nl-vps-deploy-from-windows.ps1 -VpsHost YOUR_IP"
