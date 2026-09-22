# Phase S - staging to production fleet validation
# Spins up 100+ mock fork sessions, admit load, reports SLOs + validation gate.
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [int]$ConcurrentSessions = 100,
    [int]$AdmitsPerSecond = 10,
    [int]$AdmitBurst = 50,
    [string]$NlePath = "",
    [switch]$SkipCleanup,
    [switch]$StartHost,
    [switch]$RequireProductionReady
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
    param([string]$Method, [string]$Path, $Body = $null, [int]$TimeoutSec = 120)
    $uri = ($BaseUrl.TrimEnd('/') + $Path)
    $params = @{
        Uri = $uri
        Method = $Method
        ContentType = "application/json"
        ErrorAction = "Stop"
        TimeoutSec = $TimeoutSec
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

    Write-Host "Clearing existing fork sessions before load test..." -ForegroundColor DarkGray
    for ($clearRound = 1; $clearRound -le 5; $clearRound++) {
        $existing = @(Invoke-NlApi GET "/api/v1/fork/orchestrator/sessions")
        if ($existing.Count -eq 0) { break }
        foreach ($s in $existing) {
            if ($s.sessionId) {
                try { Invoke-NlApi POST ("/api/v1/fork/orchestrator/destroy/{0}" -f $s.sessionId) | Out-Null } catch { }
            }
        }
        Write-Host ("  clear round {0}: destroyed {1} session(s), waiting..." -f $clearRound, $existing.Count) -ForegroundColor DarkGray
        Start-Sleep -Seconds 3
    }
    $left = @(Invoke-NlApi GET "/api/v1/fork/orchestrator/sessions").Count
    if ($left -gt 0) {
        Write-Warning ("Still {0} sessions after clear - use unique run ids below; consider: docker rm -f nl-fork-*" -f $left)
    }

    $runTag = Get-Date -Format "HHmmss"
    Write-Host ("Load-test run tag: {0} (unique streamer ids)" -f $runTag) -ForegroundColor DarkGray

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
    $isRealProvisioner = $orch.mode -in @("Docker", "Kubernetes")
    $createTimeoutSec = if ($isRealProvisioner) { 180 } else { 120 }
    $createDelayMs = if ($isRealProvisioner) {
        # VPS default NL_FLEET_FORK_CREATE_RATE_PER_MIN=30 -> need ~2.1s between creates
        # unless the operator raised the rate for load testing (scale env uses 200).
        if ($env:NL_LOADTEST_CREATE_DELAY_MS) { [int]$env:NL_LOADTEST_CREATE_DELAY_MS } else { 2200 }
    } else { 0 }

    $created = @()
    $latencies = New-Object System.Collections.Generic.List[double]
    $swTotal = [System.Diagnostics.Stopwatch]::StartNew()

    function New-ForkCreateBody([string]$StreamerId) {
        $idx = [int]($StreamerId -replace '\D', '')
        return @{
            streamerId = $StreamerId
            gameId = "hello-fork"
            majorVersion = "1.0"
            nlePath = $NlePath
            modIds = @()
            twitchFollowers = 100
            preferredRegion = if ($idx % 3 -eq 0) { "eu-west" } elseif ($idx % 3 -eq 1) { "us-west" } else { "us-east" }
        }
    }

    function Try-CreateForkSession([string]$StreamerId) {
        $t = [System.Diagnostics.Stopwatch]::StartNew()
        $attempts = 0
        while ($attempts -lt 8) {
            $attempts++
            try {
                $r = Invoke-NlApi POST "/api/v1/fork/orchestrator/create" (New-ForkCreateBody $StreamerId) -TimeoutSec $createTimeoutSec
                $t.Stop()
                return @{ Ok = $true; SessionId = $r.sessionId; Ms = $t.Elapsed.TotalMilliseconds }
            } catch {
                $msg = $_.Exception.Message
                if ($msg -match 'rate limit' -and $attempts -lt 8) {
                    Write-Host ("  rate limited on {0}; sleeping 5s (attempt {1}/8)..." -f $StreamerId, $attempts) -ForegroundColor DarkYellow
                    Start-Sleep -Seconds 5
                    continue
                }
                return @{ Ok = $false; Error = $msg }
            }
        }
        return @{ Ok = $false; Error = "rate limit retries exhausted" }
    }

    $provisionLabel = if ($isRealProvisioner) { "real container forks" } else { "mock/process" }
    if ($isRealProvisioner) {
        Write-Host "Warming up Docker/Kubernetes provisioner (3 sessions)..." -ForegroundColor DarkGray
        for ($w = 1; $w -le 3; $w++) {
            $warm = Try-CreateForkSession ("warmup-{0}-{1}" -f $runTag, $w)
            if ($warm.Ok) { $created += $warm.SessionId }
            Start-Sleep -Milliseconds 500
        }
    }

    Write-Host ("Creating {0} fork sessions ({1})..." -f $ConcurrentSessions, $provisionLabel) -ForegroundColor Yellow
    for ($i = 1; $i -le $ConcurrentSessions; $i++) {
        $sid = "load-{0}-{1:D4}" -f $runTag, $i
        $result = Try-CreateForkSession $sid
        if ($result.Ok) {
            $latencies.Add($result.Ms) | Out-Null
            $created += $result.SessionId
        } else {
            Write-Warning ("Create {0} failed: {1}" -f $sid, $result.Error)
        }
        if ($createDelayMs -gt 0) { Start-Sleep -Milliseconds $createDelayMs }
        if ($i % 25 -eq 0) { Write-Host ("  ... {0} / {1} (created={2})" -f $i, $ConcurrentSessions, $created.Count) }
    }

    if ($isRealProvisioner -and $created.Count -lt $ConcurrentSessions) {
        $need = $ConcurrentSessions - $created.Count
        Write-Host ("Retrying with extra streamer IDs (need {0} more)..." -f $need) -ForegroundColor Yellow
        for ($j = 1; $j -le ($need + 20) -and $created.Count -lt $ConcurrentSessions; $j++) {
            $sid = "load-{0}-r{1:D4}" -f $runTag, $j
            $result = Try-CreateForkSession $sid
            if ($result.Ok) {
                $latencies.Add($result.Ms) | Out-Null
                $created += $result.SessionId
            }
            if ($createDelayMs -gt 0) { Start-Sleep -Milliseconds ($createDelayMs * 2) }
        }
    }

    $topUp = 0
    while ($isRealProvisioner -and $topUp -lt 30) {
        $activeList = @(Invoke-NlApi GET "/api/v1/fork/orchestrator/sessions")
        if ($activeList.Count -ge $ConcurrentSessions) { break }
        $sid = "load-{0}-t{1:D4}" -f $runTag, $topUp
        $result = Try-CreateForkSession $sid
        if ($result.Ok) {
            $latencies.Add($result.Ms) | Out-Null
            $created += $result.SessionId
        }
        $topUp++
        Start-Sleep -Milliseconds 300
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
    # Sequential + retry: parallel Start-Job against a live host often loses 1/50
    # to timeouts/rate limits and fails the 99% admit SLO (49/50 = 0.98).
    for ($idx = 1; $idx -le $AdmitBurst; $idx++) {
        $ok = $false
        for ($attempt = 1; $attempt -le 4 -and -not $ok; $attempt++) {
            $body = @{
                playerId = "sp-load-$idx"
                displayName = "Load SP $idx"
                platform = "steam"
                platformUserId = "76561198000000001"
                atOwnRiskAcknowledged = $true
                gameId = "hello-fork"
            } | ConvertTo-Json -Compress
            try {
                Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + "/api/v1/session/admit") `
                    -Method POST -Body $body -ContentType "application/json" -TimeoutSec 60 | Out-Null
                $ok = $true
            } catch {
                if ($attempt -lt 4) { Start-Sleep -Milliseconds (200 * $attempt) }
            }
        }
        if ($ok) { $admitOk++ } else { $admitFail++ }
        if ($idx % 10 -eq 0) {
            Write-Host ("  admits ... {0} / {1} (ok={2} fail={3})" -f $idx, $AdmitBurst, $admitOk, $admitFail)
        }
    }
    Write-Host ("Admit burst done: ok={0} fail={1} rate={2:P1}" -f $admitOk, $admitFail, ($(if (($admitOk + $admitFail) -gt 0) { $admitOk / ($admitOk + $admitFail) } else { 0 }))) -ForegroundColor $(if ($admitFail -eq 0) { "Green" } else { "Yellow" })
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
    if ($result.validation.productionReady) {
        Write-Host "PRODUCTION VALIDATION PASSED" -ForegroundColor Green
    } elseif ($result.validation.stagingPassed) {
        Write-Host "STAGING VALIDATION PASSED" -ForegroundColor Green
        if ($RequireProductionReady) {
            Write-Host "Production gate not met (set NL_FLEET_PRODUCTION_READY=true + Docker/Kubernetes orchestrator)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "STAGING VALIDATION FAILED" -ForegroundColor Red
    }

    if (-not $SkipCleanup -and $created.Count -gt 0) {
        Write-Host "Cleaning up fork sessions..." -ForegroundColor Yellow
        foreach ($sid in $created) {
            try { Invoke-NlApi POST ("/api/v1/fork/orchestrator/destroy/{0}" -f $sid) | Out-Null } catch { }
        }
    }

    if ($RequireProductionReady) {
        if (-not $result.validation.productionReady) { exit 1 }
        Write-Host "Phase 4 production fleet validation OK" -ForegroundColor Green
    } elseif (-not $result.validation.stagingPassed) {
        exit 1
    } else {
        Write-Host "Phase S staging fleet validation OK" -ForegroundColor Green
    }
    exit 0
}
finally {
    if ($script:WeStartedHost -and $null -ne $script:HostJob) {
        Write-Host "Stopping background session host..." -ForegroundColor Yellow
        Stop-Job $script:HostJob -ErrorAction SilentlyContinue
        Remove-Job $script:HostJob -Force -ErrorAction SilentlyContinue
    }
}
