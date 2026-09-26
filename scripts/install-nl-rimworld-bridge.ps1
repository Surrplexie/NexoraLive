# Copy NL RimWorld bridge folder into a licensed RimWorld Mods directory.
param(
    [string]$RimWorldRoot = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld",
    [string]$WebSocketUrl = "wss://play.20062006.xyz/nl/v1?token=PASTE_FROM_MANIFEST",
    [string]$AdmitUrl = "https://play.20062006.xyz/api/v1/session/admit"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $Root "integrations\rimworld\mod"
$dest = Join-Path $RimWorldRoot "Mods\NLBridge"

if (-not (Test-Path $RimWorldRoot)) {
    throw "RimWorld not found at $RimWorldRoot"
}

New-Item -ItemType Directory -Force -Path (Join-Path $dest "About") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $dest "Assemblies") | Out-Null
Copy-Item (Join-Path $src "About\*") (Join-Path $dest "About") -Force

$config = @{
    websocketUrl     = $WebSocketUrl
    admitUrl         = $AdmitUrl
    gameId           = "rimworld"
    enforceJoinGate  = $true
    connectPort      = 25555
} | ConvertTo-Json
Set-Content -Path (Join-Path $dest "About\config.json") -Value $config -Encoding utf8

dotnet build (Join-Path $src "NL.RimWorld.Bridge.csproj") -c Release -v q
$dll = Join-Path $src "bin\Release\net8.0\NL.RimWorld.Bridge.dll"
if (Test-Path $dll) {
    Copy-Item $dll (Join-Path $dest "Assemblies\NL.RimWorld.Bridge.dll") -Force
}

Write-Host "Installed layout: $dest"
Write-Host "Paste live bridgeConnectUrl into About\config.json websocketUrl, then enable the mod in RimWorld."
Write-Host "In-game Harmony cancel still needs a net472 build against Assembly-CSharp (see integrations/rimworld/mod/README.md)."
