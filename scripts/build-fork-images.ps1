# Build NL fork Docker images for dogfood / staging.
param(
    [ValidateSet("hello-fork", "minecraft", "minecraft-paper", "beamng", "all")]
    [string[]]$Images = @("hello-fork"),
    [switch]$NoCache
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    throw "Docker CLI not found. Install Docker Desktop and ensure 'docker' is on PATH."
}

$map = @{
    "hello-fork" = @{
        Dockerfile = "docker/fork-hello/Dockerfile"
        Tag = "nl-fork-hello:latest"
    }
    "minecraft" = @{
        Dockerfile = "docker/fork-minecraft/Dockerfile"
        Tag = "nl-fork-minecraft:latest"
    }
    "minecraft-paper" = @{
        Dockerfile = "docker/fork-minecraft-paper/Dockerfile"
        Tag = "nl-fork-minecraft-paper:latest"
    }
    "beamng" = @{
        Dockerfile = "docker/fork-beamng/Dockerfile"
        Tag = "nl-fork-beamng:latest"
    }
}

$targets = if ($Images -contains "all") { @("hello-fork", "minecraft", "minecraft-paper", "beamng") } else { $Images }

foreach ($name in $targets) {
    $spec = $map[$name]
    if (-not $spec) {
        throw "Unknown image: $name"
    }

    $df = Join-Path $Root ($spec.Dockerfile -replace '/', '\')
    if (-not (Test-Path $df)) {
        throw "Dockerfile missing: $df"
    }

    Write-Host ("=== Building {0} ({1}) ===" -f $name, $spec.Tag) -ForegroundColor Cyan
    $args = @("build", "-f", $spec.Dockerfile, "-t", $spec.Tag, ".")
    if ($NoCache) { $args += "--no-cache" }
    & docker @args
    if ($LASTEXITCODE -ne 0) {
        throw "docker build failed for $name"
    }
    Write-Host ("OK: {0}" -f $spec.Tag) -ForegroundColor Green
}

Write-Host "Fork images built." -ForegroundColor Green
