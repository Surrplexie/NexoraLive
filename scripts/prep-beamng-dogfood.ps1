# One-command BeamNG dogfood prep: refresh bridge mod + verify NL paths.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$user = "C:\Users\surrp\AppData\Local\BeamNG\BeamNG.drive\current"
$steam = "C:\Program Files (x86)\Steam\steamapps\common\BeamNG.drive"

Write-Host "=== BeamNG dogfood prep ===" -ForegroundColor Cyan
Write-Host "User data: $user"
Write-Host "Steam game: $steam"
Write-Host ""

& "$PSScriptRoot\install-beamng-bridge.ps1"

$profile = @{
    StreamerId = "default-streamer"
    Game = "generic"
    ConfigPath = Join-Path $repo "samples\configs\beamng.nle"
    SourcePath = Join-Path $env:LOCALAPPDATA "NL\beamng-events.ndjson"
    RconEndpoint = $null
    BeamngCommandEndpoint = "127.0.0.1:27022"
    NlActionEndpoint = $null
    UseSessionBus = $false
    BusToken = $null
    AntiCheat = $true
    JoinGate = $false
    AnomalyAutoMod = $false
    UseDefaultDataPaths = $true
}
$profilePath = Join-Path $env:LOCALAPPDATA "NL\session-profile.json"
New-Item -ItemType Directory -Force -Path (Split-Path $profilePath) | Out-Null
$profile | ConvertTo-Json | Set-Content -Path $profilePath -Encoding UTF8
Write-Host "Wrote Session Host profile -> $profilePath" -ForegroundColor Green
Write-Host ""
Write-Host "NEXT (in order):" -ForegroundColor Yellow
Write-Host "  1. Fully quit BeamNG if running"
Write-Host "  2. Start BeamNG from Steam"
Write-Host "  3. Mod Manager -> enable NL_BeamNGBridge -> Apply"
Write-Host "  4. Freeroam -> pick map -> drive 30+ seconds"
Write-Host "  5. Open NL Session Host -> Game: BeamNG.drive -> Start session"
Write-Host ""
Write-Host "Verify events file grows:"
Write-Host "  Get-Content `"$($profile.SourcePath)`" -Tail 5 -Wait"
