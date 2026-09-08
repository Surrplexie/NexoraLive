# Phase T0 — scaffold a new game adapter from integrations/_template/
param(
    [Parameter(Mandatory = $true)]
    [string]$GameId,
    [string]$DisplayName = "",
    [string]$ConnectScheme = "",
    [string]$DockerImage = "",
    [string]$Major = "1.0",
    [int]$Port = 27000,
    [string]$SteamAppId = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$id = $GameId.Trim().ToLowerInvariant()
if ($id -notmatch '^[a-z0-9][a-z0-9\-]*$') {
    throw "GameId must be lowercase alphanumeric/hyphen (got '$GameId')"
}
if ($id.StartsWith("_") -or $id -eq "template") {
    throw "GameId '$id' is reserved"
}

if ([string]::IsNullOrWhiteSpace($DisplayName)) { $DisplayName = $id }
if ([string]::IsNullOrWhiteSpace($ConnectScheme)) { $ConnectScheme = $id }
if ([string]::IsNullOrWhiteSpace($DockerImage)) { $DockerImage = "nl-fork-$id" }

$dest = Join-Path $Root "integrations/$id"
if ((Test-Path $dest) -and -not $Force) {
    throw "Target already exists: $dest (pass -Force to overwrite)"
}

$template = Join-Path $Root "integrations/_template"
if (-not (Test-Path $template)) {
    throw "Missing template: $template"
}

Write-Host "=== NL Phase T0 scaffold: $id ===" -ForegroundColor Cyan

if (Test-Path $dest) {
    Remove-Item $dest -Recurse -Force
}
New-Item -ItemType Directory -Path $dest | Out-Null

function Copy-Templated([string]$Src, [string]$Dst) {
    $text = Get-Content $Src -Raw -Encoding UTF8
    $steam = if ([string]::IsNullOrWhiteSpace($SteamAppId)) { "" } else { $SteamAppId }
    $text = $text.Replace("{{GAME_ID}}", $id)
    $text = $text.Replace("{{DISPLAY_NAME}}", $DisplayName)
    $text = $text.Replace("{{CONNECT_SCHEME}}", $ConnectScheme)
    $text = $text.Replace("{{DOCKER_IMAGE}}", $DockerImage)
    $text = $text.Replace("{{MAJOR}}", $Major)
    $text = $text.Replace("{{PORT}}", "$Port")
    $text = $text.Replace("{{STEAM_APP_ID}}", $steam)
    $dir = Split-Path $Dst -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Set-Content -Path $Dst -Value $text -Encoding UTF8 -NoNewline
}

# Core integration files
Copy-Templated (Join-Path $template "README.md") (Join-Path $dest "README.md")
Copy-Templated (Join-Path $template "CHECKLIST.md") (Join-Path $dest "CHECKLIST.md")
Copy-Templated (Join-Path $template "adapter.manifest.json") (Join-Path $dest "adapter.manifest.json")
Copy-Templated (Join-Path $template "catalog.snippet.json") (Join-Path $dest "catalog.snippet.json")
Copy-Templated (Join-Path $template "sidecar/nl_sidecar.py") (Join-Path $dest "sidecar/nl_sidecar.py")

# Dockerfile under docker/fork-<id>/
$dockerDir = Join-Path $Root "docker/fork-$id"
if (-not (Test-Path $dockerDir)) { New-Item -ItemType Directory -Path $dockerDir | Out-Null }
Copy-Templated (Join-Path $template "Dockerfile") (Join-Path $dockerDir "Dockerfile")

# .nle sample
$nlePath = Join-Path $Root "samples/configs/$id.nle"
Copy-Templated (Join-Path $template "samples/template.nle") $nlePath

# Dogfood script
$dogfoodPath = Join-Path $Root "scripts/nl-dogfood-flow-$id.ps1"
Copy-Templated (Join-Path $template "scripts/nl-dogfood-flow-GAME_ID.ps1") $dogfoodPath

# Catalog merge (idempotent)
$catalogPath = Join-Path $Root "samples/fork/catalog.json"
$catalog = Get-Content $catalogPath -Raw | ConvertFrom-Json
$exists = $false
foreach ($entry in $catalog.entries) {
    if ($entry.gameId -eq $id -and $entry.majorVersion -eq $Major) { $exists = $true; break }
}
if (-not $exists) {
    $snippet = Get-Content (Join-Path $dest "catalog.snippet.json") -Raw | ConvertFrom-Json
    $catalog.entries += $snippet
    $catalog | ConvertTo-Json -Depth 8 | Set-Content $catalogPath -Encoding UTF8
    Write-Host "Catalog: added $id@$Major" -ForegroundColor Green
} else {
    Write-Host "Catalog: $id@$Major already present" -ForegroundColor Yellow
}

# build-fork-images.ps1 — append map entry if missing
$buildPath = Join-Path $Root "scripts/build-fork-images.ps1"
$buildText = Get-Content $buildPath -Raw
if ($buildText -notmatch [regex]::Escape('"' + $id + '"')) {
    Write-Host "NOTE: Add '$id' to scripts/build-fork-images.ps1 ValidateSet + `$map manually if not present." -ForegroundColor Yellow
    Write-Host @"
Suggested map entry:
    `"$id`" = @{
        Dockerfile = `"docker/fork-$id/Dockerfile`"
        Tag = `"$DockerImage`:latest`"
    }
"@
} else {
    Write-Host "build-fork-images.ps1 already references $id" -ForegroundColor Green
}

# Fix empty steamAppIds array when no Steam id
$manifestPath = Join-Path $dest "adapter.manifest.json"
$manifestJson = Get-Content $manifestPath -Raw
if ([string]::IsNullOrWhiteSpace($SteamAppId)) {
    $manifestJson = $manifestJson -replace '"steamAppIds"\s*:\s*\[\s*""\s*\]', '"steamAppIds": []'
    Set-Content -Path $manifestPath -Value $manifestJson -Encoding UTF8 -NoNewline
}

Write-Host ""
Write-Host "Scaffolded:" -ForegroundColor Green
Write-Host "  $dest"
Write-Host "  $dockerDir/Dockerfile"
Write-Host "  $nlePath"
Write-Host "  $dogfoodPath"
Write-Host ""
Write-Host "Next:" -ForegroundColor Cyan
Write-Host "  1. Ensure scripts/build-fork-images.ps1 includes '$id'"
Write-Host "  2. powershell -File scripts/nl-game-adapter-validate.ps1 -GameId $id"
Write-Host "  3. Wire real dedicated server / mod into sidecar + Dockerfile"
