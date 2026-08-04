# Phase S - staging to production fleet validation
# Spins up 100+ mock fork sessions, admit load, reports SLOs + validation gate.
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [int]$ConcurrentSessions = 100,
    [int]$AdmitsPerSecond = 10,
    [int]$AdmitBurst = 50,
    [string]$NlePath = "",
    [switch]$SkipCleanup,
    [switch]$StartHost
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($NlePath)) {
    $NlePath = Join-Path $Root "samples\configs\fork-hello.nle"
}
# Host-local runs resolve to an absolute path; Docker staging uses in-container paths (e.g. /app/...).
if ($NlePath -notmatch '^/') {
    $NlePath = (Resolve-Path $NlePath).Path
}

Write-Host "=== NL Phase S staging fleet validation ===" -ForegroundColor Cyan
Write-Host "Target: $BaseUrl  sessions=$ConcurrentSessions  admitBurst=$AdmitBurst"

$script:HostJob = $null
$script:WeStartedHost = $false

function Get-ApiErrorDetail {
    param($ErrorRecord)
    if ($null -ne $ErrorRecord.Exception.Response) {
        try {
            $stream = $ErrorRecord.Exception.Response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $text = $reader.ReadToEnd()
                if (-not [string]::IsNullOrWhiteSpace($text)) {
                    return $text.Trim()
                }
            }
        } catch { }
        return "HTTP $($ErrorRecord.Exception.Response.StatusCode.value__)"
    }
    return $ErrorRecord.Exception.Message
}

function Test-PortOpen {
    param([int]$Port = 27020)
    try {
        return (Test-NetConnection -ComputerName 127.0.0.1 -Port $Port -WarningAction SilentlyContinue).TcpTestSucceeded
    } catch {
        return $false
    }
}

function Test-SessionHostReady {
    param([int]$TimeoutSec = 5)
    $uri = ($BaseUrl.TrimEnd('/') + "/health")
    try {
        Invoke-RestMethod -Uri $uri -Method GET -TimeoutSec $TimeoutSec | Out-Null
        return $true
    } catch {
        return $false
    }
}

function Wait-SessionHostReady {
    param([int]$TimeoutSec = 120)
    Write-Host ("Waiting for session host at {0} (up to {1}s)..." -f $BaseUrl, $TimeoutSec) -ForegroundColor Yellow
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (Test-SessionHostReady -TimeoutSec 3) {
            Write-Host "Session host is up." -ForegroundColor Green
            return $true
        }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Start-SessionHostIfNeeded {
    if (Test-SessionHostReady -TimeoutSec 2) {
        Write-Host "Session host already running." -ForegroundColor Green
        return
    }

    if (-not $StartHost) {
        Write-Host ""
        Write-Host "Session host is not reachable at $BaseUrl" -ForegroundColor Red
        if (Test-PortOpen -Port 27020) {
            Write-Host "Port 27020 is open but /health did not respond - wrong service or host still starting?" -ForegroundColor Yellow
        } else {
            Write-Host "Nothing is listening on port 27020." -ForegroundColor Yellow
        }
        Write-Host ""
        Write-Host "Start the session host in another PowerShell window, then re-run this script:" -ForegroundColor Yellow
        Write-Host ("  cd {0}" -f $Root) -ForegroundColor Gray
        Write-Host '  $env:NL_FLEET_ENABLED = "true"' -ForegroundColor Gray
        Write-Host '  $env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"' -ForegroundColor Gray
        Write-Host '  $env:NL_FLEET_FORK_CREATE_RATE_PER_MIN = "200"' -ForegroundColor Gray
        Write-Host '  $env:NL_FLEET_MAX_FORK_CREATES_PER_HOUR = "9999"' -ForegroundColor Gray
        Write-Host '  $env:NL_FORK_ORCHESTRATOR_ENABLED = "true"' -ForegroundColor Gray
        Write-Host '  $env:NL_FORK_ORCHESTRATOR_MODE = "mock"' -ForegroundColor Gray
        Write-Host "  dotnet run --project src/NL.SessionHost.Web" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Wait until you see 'Now listening on http://0.0.0.0:27020', then run:" -ForegroundColor Yellow
        Write-Host "  powershell -File scripts/nl-fleet-staging-validation.ps1 -ConcurrentSessions 100" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Quick check: Invoke-RestMethod http://127.0.0.1:27020/health" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "Or pass -StartHost (stop any existing session host first to avoid file locks)." -ForegroundColor Yellow
        throw "Session host not running at $BaseUrl"
    }

    if (Test-PortOpen -Port 27020) {
        throw "Port 27020 is in use but /health failed. Stop the existing NL.SessionHost.Web process, then retry with -StartHost."
    }

    Write-Host "Building session host (Release)..." -ForegroundColor Yellow
    dotnet build src/NL.SessionHost.Web/NL.SessionHost.Web.csproj -c Release --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed - stop any running NL.SessionHost.Web and retry."
    }

    Write-Host "Starting session host in background..." -ForegroundColor Yellow
    $script:HostJob = Start-Job -Name "nl-session-host" -ScriptBlock {
        param($ProjectRoot)
        Set-Location $ProjectRoot
        $env:NL_FLEET_ENABLED = "true"
        $env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
        $env:NL_FLEET_FORK_CREATE_RATE_PER_MIN = "200"
        $env:NL_FLEET_MAX_FORK_CREATES_PER_HOUR = "9999"
        $env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
        $env:NL_FORK_ORCHESTRATOR_MODE = "mock"
        dotnet run --project src/NL.SessionHost.Web -c Release --no-build
    } -ArgumentList $Root

    $script:WeStartedHost = $true
    if (-not (Wait-SessionHostReady -TimeoutSec 180)) {
        Write-Host "Session host job output:" -ForegroundColor Red
        Receive-Job $script:HostJob -Keep | Write-Host
        throw "Session host did not become ready within 180s"
    }
}

function Invoke-NlApi {
    param([string]$Method, [string]$Path, $Body = $null)
    $uri = ($BaseUrl.TrimEnd('/') + $Path)
    $params = @{
        Uri = $uri
        Method = $Method
        ContentType = "application/json"
        ErrorAction = "Stop"
        TimeoutSec = 120
    }
    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 6 -Compress)
    }
    try {
        return Invoke-RestMethod @params
    } catch {
        $detail = Get-ApiErrorDetail $_
        throw ("API {0} {1} failed: {2}" -f $Method, $Path, $detail)
    }
}

try {
    Start-SessionHostIfNeeded

    Invoke-NlApi GET "/health" | Out-Null

    $settings = Invoke-NlApi GET "/api/v1/fleet/settings"
    if (-not $settings.enabled) {
        Write-Warning "NL_FLEET_ENABLED is false on target - validation may not reflect production fleet ops."
    }
    $forkRate = $settings.abuse.globalForkCreatesPerMinute
    if ($forkRate -lt $ConcurrentSessions) {
        Write-Warning ("Global fork create rate is {0}/min - need >={1} for {1} sessions in one minute. Set NL_FLEET_FORK_CREATE_RATE_PER_MIN=200 on session host." -f $forkRate, $ConcurrentSessions)
    }

    $orch = Invoke-NlApi GET "/api/v1/fork/orchestrator/settings"
    Write-Host ("Orchestrator mode: {0}" -f $orch.mode)

    $created = @()
    $latencies = New-Object System.Collections.Generic.List[double]
    $swTotal = [System.Diagnostics.Stopwatch]::StartNew()

    Write-Host ("Creating {0} fork sessions (mock/process)..." -f $ConcurrentSessions) -ForegroundColor Yellow
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
            Write-Warning ("Create {0} failed: {1}" -f $sid, $_.Exception.Message)
        }
        if ($i % 25 -eq 0) { Write-Host ("  ... {0} / {1}" -f $i, $ConcurrentSessions) }
    }

    $activeList = Invoke-NlApi GET "/api/v1/fork/orchestrator/sessions"
    $activeCount = @($activeList).Count
    Write-Host ("Active fork sessions: {0}" -f $activeCount) -ForegroundColor Green

    $sorted = $latencies | Sort-Object
    $p99Idx = [Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.99) - 1)
    $forkP99 = if ($sorted.Count -gt 0) { $sorted[$p99Idx] } else { 0 }

    Write-Host ("Running admit burst ({0} requests)..." -f $AdmitBurst) -ForegroundColor Yellow
    $admitOk = 0
    $admitFail = 0
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
                Invoke-RestMethod -Uri ($url + "/api/v1/session/admit") -Method POST -Body $body -ContentType "application/json" -TimeoutSec 60 | Out-Null
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
        if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
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
            try { Invoke-NlApi POST ("/api/v1/fork/orchestrator/destroy/{0}" -f $sid) | Out-Null } catch { }
        }
    }

    if (-not $result.validation.stagingPassed) { exit 1 }
    Write-Host "Phase S staging fleet validation OK" -ForegroundColor Green
}
finally {
    if ($script:WeStartedHost -and $null -ne $script:HostJob) {
        Write-Host "Stopping background session host..." -ForegroundColor Yellow
        Stop-Job $script:HostJob -ErrorAction SilentlyContinue
        Remove-Job $script:HostJob -Force -ErrorAction SilentlyContinue
    }
}
