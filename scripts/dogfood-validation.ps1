# Automated dogfood / validation replay suite for NexoraLive.
# Run from repo root: powershell -File scripts/dogfood-validation.ps1
# Optional: -SkipPublish to skip the Release publish step (faster).
param(
    [switch]$SkipPublish,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$nlRoot = Join-Path $env:LOCALAPPDATA "NL"
$failures = @()
$results = @()

function Record-Result($Name, $Ok, $Detail) {
    $script:results += [pscustomobject]@{ Scenario = $Name; Pass = $Ok; Detail = $Detail }
    if (-not $Ok) { $script:failures += $Name }
}

function Run-NlServer {
    param(
        [string[]]$ServerArgs
    )
    Push-Location $repo
    try {
        $prev = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $out = & dotnet run --project src/NL.Server -c Release --no-build -- @ServerArgs 2>&1 | Out-String
        $ErrorActionPreference = $prev
        return @{ Exit = $LASTEXITCODE; Output = $out }
    }
    finally {
        Pop-Location
    }
}

function Expect-OutputContains {
    param($Output, [string[]]$Needles, [string]$Scenario)
    foreach ($n in $Needles) {
        if ($Output -notmatch [regex]::Escape($n)) {
            Record-Result $Scenario $false "Missing expected output: $n"
            return $false
        }
    }
    Record-Result $Scenario $true "OK"
    return $true
}

Write-Host "=== NexoraLive dogfood validation ===" -ForegroundColor Cyan
Write-Host "Repo:   $repo"
Write-Host "NL dir: $nlRoot"
Write-Host ""

Push-Location $repo
try {
    if (-not $SkipBuild) {
        Write-Host "[1/8] Build Release..." -ForegroundColor Yellow
        dotnet build src/NL.sln -c Release | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        Record-Result "Build Release" $true "OK"
    }

    Write-Host "[2/8] Unit tests..." -ForegroundColor Yellow
    dotnet test src/NL.sln -c Release --no-build --verbosity quiet | Out-Null
    if ($LASTEXITCODE -ne 0) { Record-Result "Unit tests" $false "dotnet test failed"; throw "Tests failed" }
    Record-Result "Unit tests" $true "All passed"

    Write-Host "[3/8] Anti-cheat replay..." -ForegroundColor Yellow
    $r = Run-NlServer @(
        "--game", "generic",
        "--config", "samples/configs/anti-cheat.nle",
        "--source", "samples/events/anti-cheat-sample.ndjson",
        "--replay", "--anti-cheat"
    )
    Expect-OutputContains $r.Output @(
        "anomalyImpossibleAction -> Block",
        "anomalyRateSpike -> Block",
        "anomalyTeleport -> Block"
    ) "Anti-cheat sample"

    Write-Host "[4/8] Anti-cheat + anomaly auto-mod..." -ForegroundColor Yellow
    $r = Run-NlServer @(
        "--game", "generic",
        "--config", "samples/configs/anti-cheat.nle",
        "--source", "samples/events/anti-cheat-sample.ndjson",
        "--replay", "--anti-cheat", "--anomaly-auto-mod",
        "--streamer", "default-streamer"
    )
    Expect-OutputContains $r.Output @("anomalyImpossibleAction -> Block") "Anomaly auto-mod replay"

    Write-Host "[5/8] Minecraft log replay..." -ForegroundColor Yellow
    $r = Run-NlServer @(
        "--game", "minecraft",
        "--config", "samples/configs/minecraft.nle",
        "--source", "samples/logs/minecraft-sample.log",
        "--replay"
    )
    Expect-OutputContains $r.Output @(
        "playerChat -> Block",
        "please avoid excessive caps"
    ) "Minecraft sample log"

    Write-Host "[6/8] BeamNG replay..." -ForegroundColor Yellow
    $r = Run-NlServer @(
        "--game", "generic",
        "--config", "samples/configs/beamng.nle",
        "--source", "samples/events/beamng-sample.ndjson",
        "--replay", "--anti-cheat"
    )
    Expect-OutputContains $r.Output @(
        "speed limit",
        "hard crash",
        "rollover"
    ) "BeamNG sample"

    Write-Host "[7/8] Join gate (seed banned Eve)..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $nlRoot | Out-Null
    $profilesPath = Join-Path $nlRoot "sp-profiles.json"
    Remove-Item $profilesPath -ErrorAction SilentlyContinue
    & "$PSScriptRoot\seed-banned-eve.ps1" | Out-Null
    if ($LASTEXITCODE -ne 0) { Record-Result "Join gate seed + replay" $false "seed-banned-eve.ps1 failed" }
    else {
        $jg = Get-Content $profilesPath -Raw
        $seedOk = (Test-Path $profilesPath) -and ($jg -match '"Standing": "Banned"')
        if (-not $seedOk) {
            Record-Result "Join gate seed + replay" $false "Eve not Banned in sp-profiles.json"
        }
        else {
            # Re-run join gate only to capture output (seed script already ran once)
            $r = Run-NlServer @(
                "--game", "generic",
                "--config", "samples/configs/generic.nle",
                "--source", "samples/events/join-gate-sample.ndjson",
                "--replay", "--join-gate",
                "--streamer", "default-streamer",
                "--sp-store", $profilesPath,
                "--moderation-log", (Join-Path $nlRoot "moderation.jsonl")
            )
            Expect-OutputContains $r.Output @(
                "[join:Allow]",
                "[join:Deny]",
                "SP is banned"
            ) "Join gate banned Eve"
        }
    }

    if (-not $SkipPublish) {
        Write-Host "[8/8] Publish Windows apps..." -ForegroundColor Yellow
        & "$PSScriptRoot\publish.ps1" | Out-Null
        $hostExe = Join-Path $repo "artifacts\publish\SessionHost\NL.SessionHost.exe"
        if (Test-Path $hostExe) { Record-Result "Publish" $true $hostExe }
        else { Record-Result "Publish" $false "SessionHost exe missing" }
    }
    else {
        Write-Host "[8/8] Publish skipped (-SkipPublish)" -ForegroundColor DarkGray
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Results ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize

if ($failures.Count -gt 0) {
    Write-Host "FAILED: $($failures -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "All automated dogfood checks passed." -ForegroundColor Green
Write-Host ""
Write-Host "Next: run a LIVE session - see docs/DOGFOOD_NOTES.md section 'Live session playbook'."
exit 0
