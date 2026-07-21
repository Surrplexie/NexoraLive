# Installs NL_BeamNGBridge as a zipped mod (like BeamMP.zip), not unpacked/.
# Prefers BEAMNG_USER_FOLDER, else discovers the modern or legacy user-data layouts.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$src = Join-Path $repo "beamng-mod\NL_BeamNGBridge"

function Resolve-BeamNgUserFolder {
    if ($env:BEAMNG_USER_FOLDER -and (Test-Path $env:BEAMNG_USER_FOLDER)) {
        return (Resolve-Path $env:BEAMNG_USER_FOLDER).Path
    }

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
    exit 1
}

$modsDir = Join-Path $user "mods"
$zipPath = Join-Path $modsDir "NL_BeamNGBridge.zip"
$unpackedPath = Join-Path $modsDir "unpacked\NL_BeamNGBridge"
$staging = Join-Path $env:TEMP "NL_BeamNGBridge_pack"

New-Item -ItemType Directory -Force -Path $modsDir | Out-Null
if (Test-Path $staging) {
    Remove-Item -Recurse -Force $staging
}
Copy-Item -Recurse -Force $src $staging
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
if (Test-Path $unpackedPath) {
    Remove-Item -Recurse -Force $unpackedPath
}
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -Force
Remove-Item -Recurse -Force $staging

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

Write-Host "Installed bridge zip -> $zipPath"
Write-Host "Removed old unpacked -> $unpackedPath (if it existed)"
Write-Host "Events file          -> $events"
Write-Host "Kick queue           -> $kicks"
Write-Host "In BeamNG: Escape -> Mods -> enable NL_BeamNGBridge (zip), load a map, then NL Session Host -> Load BeamNG freeroam defaults."
Write-Host "Verify in beamng.log: 'NL_BeamNGBridge loaded'"
