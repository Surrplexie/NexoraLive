# Installs NL_Kick into a BeamMP server Resources/Server folder.
# Usage:
#   powershell -File scripts/install-beammp-nl-kick.ps1
#   powershell -File scripts/install-beammp-nl-kick.ps1 -ServerRoot "D:\BeamMP-Server"
param(
    [string]$ServerRoot = $env:BEAMMP_SERVER_ROOT
)

$ErrorActionPreference = "Stop"

$repo = Split-Path $PSScriptRoot -Parent
$src = Join-Path $repo "beamng-mod\NL_BeamMPServer\NL_Kick"

if (-not $ServerRoot -or -not (Test-Path $ServerRoot)) {
    Write-Host "Set -ServerRoot or BEAMMP_SERVER_ROOT to your BeamMP server directory."
    Write-Host "Manual: copy $src to <server>\Resources\Server\NL_Kick"
    exit 1
}

$dest = Join-Path $ServerRoot "Resources\Server\NL_Kick"
New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
if (Test-Path $dest) {
    Remove-Item -Recurse -Force $dest
}
Copy-Item -Recurse -Force $src $dest

$nl = Join-Path $env:LOCALAPPDATA "NL"
New-Item -ItemType Directory -Force -Path $nl | Out-Null
$kicks = Join-Path $nl "beamng-kicks.ndjson"
if (-not (Test-Path $kicks)) {
    Set-Content -Path $kicks -Value "# NL BeamMP kick queue`n" -Encoding UTF8
}

Write-Host "Installed NL_Kick → $dest"
Write-Host "Queue file        → $kicks"
Write-Host "Override path with env NL_BEAMNG_KICKS if the server runs as another user."
