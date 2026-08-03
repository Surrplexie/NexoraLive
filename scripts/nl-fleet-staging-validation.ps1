# Phase S — staging → production fleet validation
# Spins up 100+ mock fork sessions, admit load, reports SLOs + validation gate.
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [int]$ConcurrentSessions = 100,
    [int]$AdmitsPerSecond = 10,
    [int]$AdmitBurst = 50,
    [string]$NlePath = "",
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($NlePath)) {
    $NlePath = Join-Path $Root "samples\configs\fork-hello.nle"
}
$NlePath = (Resolve-Path $NlePath).Path

Write-Host "=== NL Phase S staging fleet validation ===" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl  sessions=$ConcurrentSessions  admitBurst=$AdmitBurst"

function Invoke-NlApi {
    param([string]$Method, [string]$Path, $Body = $null)
    $uri = ($BaseUrl.TrimEnd('/') + $Path)
    $params = @{
        Uri = $uri
        Method = $Method
        ContentType = "application/json"
        ErrorAction = "Stop"
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 6 -Compress)
    }
    try {
        return Invoke-RestMethod @params
    } catch {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $detail = $reader.ReadToEnd()
        throw "API $Method $Path failed: $detail"
    }
}

# Health
Invoke-NlApi GET "/health" | Out-Null

$settings = Invoke-NlApi GET "/api/v1/fleet/settings"
if (-not $settings.enabled) {
    Write-Warning "NL_FLEET_ENABLED is false on target — validation may not reflect production fleet ops."
}

$orch = Invoke-NlApi GET "/api/v1/fork/orchestrator/settings"
Write-Host "Orchestrator mode: $($orch.mode)"

$created = @()
$latencies = New-Object System.Collections.Generic.List[double]
$swTotal = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host "Creating $ConcurrentSessions fork sessions (mock/process)..." -ForegroundColor Yellow
for ($i = 1; $i -le $ConcurrentSessions; $i++) {
    $sid = "load-{0:D4}" -f $i
    $body = @{
        streamerId = $sid
        gameId = "hello-fork"
        majorVersion = "1.0"
        nlePath = $NlePath
        modIds = @()
        twitchFollowers = 100
        preferredRegion = if ($i % 3 -eq 0) { "eu-west" } elseif ($i % 3 -eq 1) { "us-west" } else { "us-east" }
    }
    $t = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $r = Invoke-NlApi POST "/api/v1/fork/orchestrator/create" $body
        $t.Stop()
        $latencies.Add($t.Elapsed.TotalMilliseconds) | Out-Null
        $created += $r.sessionId
    } catch {
        Write-Warning "Create $sid failed: $_"
    }
    if ($i % 25 -eq 0) { Write-Host "  ... $i / $ConcurrentSessions" }
}

$activeList = Invoke-NlApi GET "/api/v1/fork/orchestrator/sessions"
$activeCount = @($activeList).Count
Write-Host "Active fork sessions: $activeCount" -ForegroundColor Green

$sorted = $latencies | Sort-Object
$p99Idx = [Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.99) - 1)
$forkP99 = if ($sorted.Count -gt 0) { $sorted[$p99Idx] } else { 0 }

Write-Host "Running admit burst ($AdmitBurst requests)..." -ForegroundColor Yellow
$admitOk = 0
$admitFail = 0
$admitSw = [System.Diagnostics.Stopwatch]::StartNew()
$jobs = 1..$AdmitBurst | ForEach-Object {
    Start-Job -ScriptBlock {
        param($url, $idx)
        $body = @{
            playerId = "sp-load-$idx"
            displayName = "Load SP $idx"
            platform = "steam"
            platformUserId = "76561198000000001"
        } | ConvertTo-Json -Compress
        try {
            Invoke-RestMethod -Uri ($url + "/api/v1/session/admit") -Method POST -Body $body -ContentType "application/json" | Out-Null
            return $true
        } catch {
            return $false
        }
    } -ArgumentList $BaseUrl, $_
}
$jobs | Wait-Job | Out-Null
foreach ($j in $jobs) {
    if (Receive-Job $j) { $admitOk++ } else { $admitFail++ }
    Remove-Job $j
}
$admitSw.Stop()
$swTotal.Stop()

$reportBody = @{
    concurrentSessionsTarget = $ConcurrentSessions
    admitsPerSecondTarget = $AdmitsPerSecond
    admitsSucceeded = $admitOk
    admitsFailed = $admitFail
    elapsedSeconds = $swTotal.Elapsed.TotalSeconds
    activeForkSessions = $activeCount
    activeNlsSessions = 0
    forkCreateP99Ms = $forkP99
}

Write-Host "Reporting load test + validation..." -ForegroundColor Yellow
$result = Invoke-NlApi POST "/api/v1/fleet/load-test/report" $reportBody

Write-Host ""
Write-Host "SLO results:" -ForegroundColor Cyan
foreach ($s in $result.slos) {
    $mark = if ($s.met) { "PASS" } else { "FAIL" }
    $color = if ($s.met) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}: current={2} target={3} {4}" -f $mark, $s.name, $s.current, $s.target, $s.unit) -ForegroundColor $color
}

Write-Host ""
Write-Host "Validation checks:" -ForegroundColor Cyan
foreach ($c in $result.validation.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host "       $($c.detail)" -ForegroundColor DarkGray }
}

Write-Host ""
if ($result.validation.stagingPassed) {
    Write-Host "STAGING VALIDATION PASSED" -ForegroundColor Green
} else {
    Write-Host "STAGING VALIDATION FAILED" -ForegroundColor Red
}

if (-not $SkipCleanup -and $created.Count -gt 0) {
    Write-Host "Cleaning up fork sessions..." -ForegroundColor Yellow
    foreach ($sid in $created) {
        try { Invoke-NlApi POST "/api/v1/fork/orchestrator/destroy/$sid" | Out-Null } catch { }
    }
}

if (-not $result.validation.stagingPassed) { exit 1 }
Write-Host "Phase S staging fleet validation OK" -ForegroundColor Green
