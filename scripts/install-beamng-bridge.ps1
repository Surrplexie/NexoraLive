# Installs NL_BeamNGBridge as a proper BeamNG zip (forward-slash paths) + unpacked copy.
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
        if ($withMods) { return $withMods.FullName }
    }
    $root = Join-Path $env:LOCALAPPDATA "BeamNG.drive"
    if (Test-Path $root) {
        $versionDirs = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d+\.\d+' } |
            Sort-Object { $_.Name } -Descending
        foreach ($dir in $versionDirs) { return $dir.FullName }
        if (Test-Path (Join-Path $root "mods")) { return (Resolve-Path $root).Path }
    }
    return $null
}

function Write-BeamNgZip([string]$SourceDir, [string]$ZipPath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $ZipPath) { Remove-Item -Force $ZipPath }
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem $SourceDir -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($SourceDir.Length).TrimStart('\', '/').Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $zip.Dispose()
    }
}

$user = Resolve-BeamNgUserFolder
if (-not $user) {
    Write-Host "Could not find BeamNG user folder under %LOCALAPPDATA%\BeamNG\BeamNG.drive\current"
    exit 1
}

$modsDir = Join-Path $user "mods"
$zipPath = Join-Path $modsDir "NL_BeamNGBridge.zip"
$unpackedPath = Join-Path $modsDir "unpacked\NL_BeamNGBridge"

New-Item -ItemType Directory -Force -Path $modsDir | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $unpackedPath) | Out-Null
if (Test-Path $unpackedPath) { Remove-Item -Recurse -Force $unpackedPath }
Copy-Item -Recurse -Force $src $unpackedPath
Write-BeamNgZip -SourceDir $unpackedPath -ZipPath $zipPath
Remove-Item -Recurse -Force $unpackedPath

$nl = Join-Path $env:LOCALAPPDATA "NL"
New-Item -ItemType Directory -Force -Path $nl | Out-Null
$events = Join-Path $nl "beamng-events.ndjson"
if (-not (Test-Path $events)) {
    Set-Content -Path $events -Value "# NL BeamNG events (appended by NL_BeamNGBridge)`n" -Encoding UTF8
}
$kicks = Join-Path $nl "beamng-kicks.ndjson"
if (-not (Test-Path $kicks)) {
    Set-Content -Path $kicks -Value "# NL BeamMP kick queue`n" -Encoding UTF8
}

Write-Host "Installed zip (forward slashes) -> $zipPath"
Write-Host "Events file                   -> $events"
Write-Host ""
Write-Host "NEXT: fully quit BeamNG, restart, enable NL_BeamNGBridge, load a map."
Write-Host "Then run in PowerShell (not CMD):"
Write-Host "  findstr /i ""NL_BeamNGBridge loaded"" ""$user\beamng.log"""
Write-Host "  type ""$events"""
