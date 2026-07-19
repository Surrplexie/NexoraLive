# Installs NL_BeamNGBridge into the BeamNG.drive user mods/unpacked folder.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$src = Join-Path $repo "beamng-mod\NL_BeamNGBridge"

# BeamNG user folder is typically under Local AppData (versioned). Allow override.
$candidates = @(
    $env:BEAMNG_USER_FOLDER,
    (Join-Path $env:LOCALAPPDATA "BeamNG.drive"),
    (Join-Path $env:LOCALAPPDATA "BeamNG.drive\0.34"),
    (Join-Path $env:LOCALAPPDATA "BeamNG.drive\0.35"),
    (Join-Path $env:LOCALAPPDATA "BeamNG.drive\0.36")
) | Where-Object { $_ -and (Test-Path $_) }

if (-not $candidates -or $candidates.Count -eq 0) {
    Write-Host "Could not find BeamNG user folder. Set BEAMNG_USER_FOLDER to your BeamNG user directory."
    Write-Host "Manual install: copy $src to <BeamNG user>\mods\unpacked\NL_BeamNGBridge"
    exit 1
}

$user = $candidates[0]
$destRoot = Join-Path $user "mods\unpacked\NL_BeamNGBridge"
New-Item -ItemType Directory -Force -Path (Split-Path $destRoot) | Out-Null
if (Test-Path $destRoot) {
    Remove-Item -Recurse -Force $destRoot
}
Copy-Item -Recurse -Force $src $destRoot

# Ensure NL data dir exists for NDJSON output
$nl = Join-Path $env:LOCALAPPDATA "NL"
New-Item -ItemType Directory -Force -Path $nl | Out-Null
$events = Join-Path $nl "beamng-events.ndjson"
if (-not (Test-Path $events)) {
    Set-Content -Path $events -Value "# NL BeamNG events (appended by NL_BeamNGBridge)`n" -Encoding UTF8
}

Write-Host "Installed bridge → $destRoot"
Write-Host "Events file      → $events"
Write-Host "Enable the mod in BeamNG (Escape → Mods), load a map, then start NL Session Host with game=generic."
