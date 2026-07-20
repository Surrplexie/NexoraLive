# Installs NL_BeamNGBridge into the BeamNG.drive user mods/unpacked folder.
# Prefers BEAMNG_USER_FOLDER, else discovers the modern or legacy user-data layouts.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$src = Join-Path $repo "beamng-mod\NL_BeamNGBridge"

function Resolve-BeamNgUserFolder {
    if ($env:BEAMNG_USER_FOLDER -and (Test-Path $env:BEAMNG_USER_FOLDER)) {
        return (Resolve-Path $env:BEAMNG_USER_FOLDER).Path
    }

    # BeamNG 0.38+ (Steam): %LOCALAPPDATA%\BeamNG\BeamNG.drive\current
    $modernCurrent = Join-Path $env:LOCALAPPDATA "BeamNG\BeamNG.drive\current"
    if (Test-Path (Join-Path $modernCurrent "mods")) {
        return (Resolve-Path $modernCurrent).Path
    }

    $modernRoot = Join-Path $env:LOCALAPPDATA "BeamNG\BeamNG.drive"
    if (Test-Path $modernRoot) {
        $withMods = Get-ChildItem $modernRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.FullName "mods") } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($withMods) {
            return $withMods.FullName
        }
    }

    # Legacy: %LOCALAPPDATA%\BeamNG.drive\<version>
    $root = Join-Path $env:LOCALAPPDATA "BeamNG.drive"
    if (-not (Test-Path $root)) {
        return $null
    }

    $versionDirs = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d+\.\d+' } |
        Sort-Object {
            $parts = $_.Name.Split('.')
            [int]$parts[0] * 10000 + [int]$parts[1] * 100 + ($(if ($parts.Length -gt 2) { [int]$parts[2] } else { 0 }))
        } -Descending

    foreach ($dir in $versionDirs) {
        return $dir.FullName
    }

    if (Test-Path (Join-Path $root "mods")) {
        return (Resolve-Path $root).Path
    }

    return $null
}

$user = Resolve-BeamNgUserFolder
if (-not $user) {
    Write-Host "Could not find BeamNG user folder. Set BEAMNG_USER_FOLDER to your BeamNG user directory"
    Write-Host "(the folder that contains mods\, usually under %LOCALAPPDATA%\BeamNG\BeamNG.drive\current)."
    Write-Host "Steam game folder is NOT the user folder: C:\Program Files (x86)\Steam\steamapps\common\BeamNG.drive"
    Write-Host "Manual install: copy $src to <BeamNG user>\mods\unpacked\NL_BeamNGBridge"
    exit 1
}

$destRoot = Join-Path $user "mods\unpacked\NL_BeamNGBridge"
New-Item -ItemType Directory -Force -Path (Split-Path $destRoot) | Out-Null
if (Test-Path $destRoot) {
    Remove-Item -Recurse -Force $destRoot
}
Copy-Item -Recurse -Force $src $destRoot

# Ensure NL data dir exists for NDJSON output + kick queue
$nl = Join-Path $env:LOCALAPPDATA "NL"
New-Item -ItemType Directory -Force -Path $nl | Out-Null
$events = Join-Path $nl "beamng-events.ndjson"
if (-not (Test-Path $events)) {
    Set-Content -Path $events -Value "# NL BeamNG events (appended by NL_BeamNGBridge)`n" -Encoding UTF8
}
$kicks = Join-Path $nl "beamng-kicks.ndjson"
if (-not (Test-Path $kicks)) {
    Set-Content -Path $kicks -Value "# NL BeamMP kick queue (appended by bridge; consumed by NL_Kick server plugin)`n" -Encoding UTF8
}

Write-Host "Installed bridge -> $destRoot"
Write-Host "Events file      -> $events"
Write-Host "Kick queue       -> $kicks"
Write-Host "Enable the mod in BeamNG (Escape -> Mods), load a map, then start NL Session Host with Tools -> Load BeamNG freeroam defaults."
Write-Host "If nothing happens: open beamng.log and search for 'NL_BeamNGBridge loaded' (mod needs scripts/NL_BeamNGBridge/modScript.lua)."
Write-Host "BeamMP note: Launcher Resources is client vehicle packs. NL_Kick goes on a BeamMP Server (Resources/Server/NL_Kick), not BeamMP-Launcher."
