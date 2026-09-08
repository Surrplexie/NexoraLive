# Phase T0 — validate a game fork adapter against the onboarding checklist.
param(
    [Parameter(Mandatory = $false)]
    [string]$GameId = "",
    [switch]$All,
    [switch]$SkipUnitTests
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if (-not $All -and [string]::IsNullOrWhiteSpace($GameId)) {
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  powershell -File scripts/nl-game-adapter-validate.ps1 -GameId example-game"
    Write-Host "  powershell -File scripts/nl-game-adapter-validate.ps1 -All"
    exit 2
}

Write-Host "=== NL Phase T0 game adapter validate ===" -ForegroundColor Cyan

if (-not $SkipUnitTests) {
    Write-Host "Unit tests (GameForkAdapter*)..." -ForegroundColor Cyan
    dotnet test tests/NL.Fork.Core.Tests/NL.Fork.Core.Tests.csproj -c Release --filter "FullyQualifiedName~GameForkAdapter" --verbosity quiet
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Get-AdapterIds {
    $ids = @()
    Get-ChildItem (Join-Path $Root "integrations") -Directory | ForEach-Object {
        $name = $_.Name
        if ($name.StartsWith("_")) { return }
        if ($name -in @("python", "nodejs", "lua", "dotnet", "unity", "unreal", "godot", "rust", "fork", "generic")) { return }
        $manifest = Join-Path $_.FullName "adapter.manifest.json"
        if (Test-Path $manifest) {
            $ids += $name
        }
    }
    return $ids
}

$targets = if ($All) { Get-AdapterIds } else { @($GameId.Trim()) }
if ($targets.Count -eq 0) {
    throw "No adapter manifests found under integrations/"
}

$failed = @()

foreach ($id in $targets) {
    Write-Host ("--- {0} ---" -f $id) -ForegroundColor Cyan
    $manifestPath = Join-Path $Root "integrations/$id/adapter.manifest.json"
    if (-not (Test-Path $manifestPath)) {
        Write-Host "FAIL: missing $manifestPath" -ForegroundColor Red
        $failed += $id
        continue
    }

    $m = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $checks = @()

    function Add-Check([string]$Name, [bool]$Ok, [string]$Detail) {
        $script:checks += [pscustomobject]@{ Name = $Name; Ok = $Ok; Detail = $Detail }
        $color = if ($Ok) { "Green" } else { "Red" }
        $mark = if ($Ok) { "PASS" } else { "FAIL" }
        Write-Host ("  [{0}] {1}: {2}" -f $mark, $Name, $Detail) -ForegroundColor $color
    }

    $catalog = Get-Content (Join-Path $Root "samples/fork/catalog.json") -Raw
    $catalogOk = $catalog -match ('"gameId"\s*:\s*"' + [regex]::Escape($m.gameId) + '"')
    Add-Check "catalog" $catalogOk ("gameId={0}" -f $m.gameId)

    $df = Join-Path $Root (($m.dockerfile -replace '/', [IO.Path]::DirectorySeparatorChar))
    Add-Check "dockerfile" (Test-Path $df) $m.dockerfile

    $buildScript = Get-Content (Join-Path $Root "scripts/build-fork-images.ps1") -Raw
    $buildKey = if ($m.buildImageKey) { $m.buildImageKey } else { $m.gameId }
    $buildOk = $buildScript.Contains('"' + $buildKey + '"')
    Add-Check "build-script" $buildOk $buildKey

    $integration = Join-Path $Root (($m.integrationDir -replace '/', [IO.Path]::DirectorySeparatorChar))
    Add-Check "integration" ((Test-Path $integration) -and (Test-Path $manifestPath)) $m.integrationDir

    $nleRel = $m.defaultNleTemplate -replace '\\', '/'
    if ($nleRel.StartsWith("configs/")) { $nleRel = "samples/" + $nleRel }
    elseif (-not $nleRel.StartsWith("samples/")) { $nleRel = "samples/" + $nleRel.TrimStart('/') }
    $nlePath = Join-Path $Root ($nleRel -replace '/', [IO.Path]::DirectorySeparatorChar)
    $nleOk = Test-Path $nlePath
    Add-Check "nle" $nleOk $nleRel

    if ($nleOk) {
        $nleText = Get-Content $nlePath -Raw
        $missing = @()
        foreach ($ev in $m.requiredEvents) {
            if ($nleText -notmatch ("(?m)^event\s+" + [regex]::Escape($ev) + "\s*:")) {
                $missing += $ev
            }
        }
        Add-Check "nle-events" ($missing.Count -eq 0) ($(if ($missing.Count -eq 0) { "all present" } else { "missing: " + ($missing -join ", ") }))
    } else {
        Add-Check "nle-events" $false "skipped"
    }

    $dogfood = Join-Path $Root (($m.dogfoodScript -replace '/', [IO.Path]::DirectorySeparatorChar))
    Add-Check "dogfood" (Test-Path $dogfood) $m.dogfoodScript

    $readmeOk = (Test-Path (Join-Path $integration "README.md")) -or (Test-Path (Join-Path $integration "CHECKLIST.md")) -or (Test-Path (Join-Path $integration "sidecar/nl_sidecar.py"))
    Add-Check "smoke-docs" $readmeOk "README/CHECKLIST/sidecar"

    $actions = @($m.requiredActions)
    $actionsOk = ($actions -contains "warn") -and ($actions -contains "kick")
    Add-Check "actions" $actionsOk ($actions -join ", ")

    Add-Check "connect-scheme" (-not [string]::IsNullOrWhiteSpace($m.connectScheme)) ("{0}://" -f $m.connectScheme)

    $healthOk = $null -ne $m.health -and -not [string]::IsNullOrWhiteSpace([string]$m.health.type)
    $healthDetail = if ($healthOk) { "{0}:{1}" -f $m.health.type, $m.health.path } else { "missing" }
    Add-Check "health" $healthOk $healthDetail

    if ($m.nativePlugin) {
        $pluginRel = [string]$m.nativePlugin
        $pluginAbs = Join-Path $Root ($pluginRel -replace '/', [IO.Path]::DirectorySeparatorChar)
        $about = Join-Path $pluginAbs "About/About.xml"
        $pom = Join-Path $pluginAbs "pom.xml"
        $pluginYml = Join-Path $pluginAbs "src/main/resources/plugin.yml"
        $pluginOk = (Test-Path $pluginAbs) -and ((Test-Path $about) -or (Test-Path $pom) -or (Test-Path $pluginYml))
        Add-Check "native-plugin" $pluginOk $pluginRel
    }

    if ($checks | Where-Object { -not $_.Ok }) {
        $failed += $id
    } else {
        Write-Host ("OK: {0}" -f $id) -ForegroundColor Green
    }
}

if ($failed.Count -gt 0) {
    Write-Host ("Adapter validate FAILED: {0}" -f ($failed -join ", ")) -ForegroundColor Red
    exit 1
}

Write-Host "Phase T0 game adapter validate OK" -ForegroundColor Green
