# End-to-end dogfood: setup → start → client join → teardown
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$StreamerId = "dogfood-streamer",
    [string]$PlayerId = "sp-dogfood-1",
    [string]$Steam64 = "76561198000000001",
    [string]$GameId = "hello-fork",
    [string]$MajorVersion = "1.0",
    [ValidateSet("mock", "process", "docker", "auto")]
    [string]$ExpectProvisioner = "mock",
    [switch]$SkipImageBuild,
    [switch]$VerifyRuleEvents,
    [int]$TeardownGraceSec = 30,
    [int]$TeardownPollTimeoutSec = 75
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

function Invoke-Nl {
    param([string]$Method, [string]$Path, $Body = $null)
    $uri = $BaseUrl.TrimEnd('/') + $Path
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
        $msg = $_.Exception.Message
        if ($_.Exception.Response) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $detail = $reader.ReadToEnd()
                if ($detail) { $msg = $detail }
            } catch { }
        }
        throw ("{0} {1} failed: {2}" -f $Method, $Path, $msg)
    }
}

function Step([string]$Name, [scriptblock]$Action) {
    Write-Host ("--- {0} ---" -f $Name) -ForegroundColor Cyan
    & $Action
    Write-Host ("OK: {0}" -f $Name) -ForegroundColor Green
}

function Get-DockerImageBuild([string]$Id) {
    switch ($Id.Trim().ToLowerInvariant()) {
        "minecraft" { return @{ Dockerfile = "docker/fork-minecraft/Dockerfile"; Tag = "nl-fork-minecraft:latest" } }
        "minecraft-paper" { return @{ Dockerfile = "docker/fork-minecraft-paper/Dockerfile"; Tag = "nl-fork-minecraft-paper:latest" } }
        "beamng" { return @{ Dockerfile = "docker/fork-beamng/Dockerfile"; Tag = "nl-fork-beamng:latest" } }
        default { return @{ Dockerfile = "docker/fork-hello/Dockerfile"; Tag = "nl-fork-hello:latest" } }
    }
}

$shouldVerifyRules = $VerifyRuleEvents.IsPresent -or ($GameId -eq "minecraft")

Write-Host "=== NL dogfood flow ===" -ForegroundColor Cyan
Write-Host ("Target: {0}" -f $BaseUrl)
Write-Host ("Game: {0}" -f $GameId) -ForegroundColor DarkGray
Write-Host ("Expect provisioner: {0}" -f $ExpectProvisioner) -ForegroundColor DarkGray

if ($ExpectProvisioner -eq "process") {
    $runtimeDll = Join-Path $Root "src\NL.Fork.Runtime\bin\Release\net8.0\NL.Fork.Runtime.dll"
    if (-not (Test-Path $runtimeDll)) {
        Write-Host "Building NL.Fork.Runtime (Release) for process provisioner..." -ForegroundColor DarkGray
        dotnet build (Join-Path $Root "src\NL.Fork.Runtime\NL.Fork.Runtime.csproj") -c Release | Out-Null
        if (-not (Test-Path $runtimeDll)) {
            throw "NL.Fork.Runtime Release build missing. Run: dotnet build src/NL.Fork.Runtime -c Release"
        }
    }
}

if ($ExpectProvisioner -eq "docker" -and -not $SkipImageBuild) {
    $img = Get-DockerImageBuild $GameId
    Write-Host ("Building Docker image {0} ..." -f $img.Tag) -ForegroundColor DarkGray
    docker build -f $img.Dockerfile -t $img.Tag .
    if ($LASTEXITCODE -ne 0) {
        throw ("docker build failed for {0}" -f $img.Tag)
    }
}

Step "Health check" {
    Invoke-Nl GET "/health" | Out-Null
}

Step "Dogfood setup (profile + mock ownership)" {
    $setup = Invoke-Nl POST "/api/v1/dogfood/setup" @{ gameId = $GameId }
    $sid = if ($setup.profile) { $setup.profile.streamerId } else { $setup.streamerId }
    if ($sid -ne $StreamerId) {
        throw ("Expected streamer {0}, got {1}" -f $StreamerId, $sid)
    }
    if ($setup.profile -and $setup.profile.gameId -and $setup.profile.gameId -ne $GameId) {
        throw ("Expected gameId {0}, got {1}" -f $GameId, $setup.profile.gameId)
    }
    if (-not $setup.profile.forkOrchestratorEnabled) {
        throw "Dogfood profile must have forkOrchestratorEnabled=true."
    }
}

Step "Start session + fork provision" {
    Invoke-Nl POST "/api/v1/session/start" @{ replayOnce = $false } | Out-Null
    Start-Sleep -Seconds 2
    $status = Invoke-Nl GET "/api/v1/dogfood/status"
    if (-not $status.sessionRunning) {
        throw "Session not running after start."
    }
    if (-not $status.forkOrchestratorEnabled) {
        throw "Fork orchestrator not enabled on profile."
    }
    if ([string]::IsNullOrWhiteSpace($status.forkSessionId)) {
        throw "No forkSessionId after start."
    }
    $forkSessions = @(Invoke-Nl GET "/api/v1/fork/orchestrator/sessions")
    $fork = $forkSessions | Where-Object { $_.sessionId -eq $status.forkSessionId } | Select-Object -First 1
    if ($null -eq $fork) {
        throw "Fork session $($status.forkSessionId) not listed by orchestrator."
    }
    $prov = [string]$fork.provisioner
    if ($ExpectProvisioner -ne "auto" -and $prov -ne ($ExpectProvisioner.Substring(0,1).ToUpper() + $ExpectProvisioner.Substring(1))) {
        throw ("Expected provisioner {0}, got {1}. Set NL_FORK_ORCHESTRATOR_MODE={0} on session host." -f $ExpectProvisioner, $prov)
    }
    Write-Host ("  forkSessionId={0} provisioner={1}" -f $status.forkSessionId, $prov) -ForegroundColor DarkGray
    if ($fork.forkConnectEndpoint) {
        Write-Host ("  forkConnect={0}" -f $fork.forkConnectEndpoint) -ForegroundColor DarkGray
    }
    if ($ExpectProvisioner -eq "docker" -and $fork.forkConnectEndpoint -notmatch '^(docker|minecraft)://') {
        throw ("Expected docker:// or minecraft:// connect URL, got {0}" -f $fork.forkConnectEndpoint)
    }
}

Step "NL Client join flow" {
    $joinBody = @{
        playerId = $PlayerId
        streamerId = $StreamerId
        platformUserId = $Steam64
        platform = "steam"
        gameId = $GameId
        majorVersion = $MajorVersion
        atOwnRiskAcknowledged = $true
        mode = "Player"
    }
    $join = Invoke-Nl POST "/api/v1/client/join-flow" $joinBody
    if (-not $join.success) {
        throw ("Join failed at step {0}: {1}" -f $join.step, $join.message)
    }
    if ($null -eq $join.launch) {
        throw "Join succeeded but launch params missing."
    }
    Write-Host ("  forkConnect={0}" -f $join.launch.forkConnectEndpoint) -ForegroundColor DarkGray
}

if ($shouldVerifyRules) {
    Step "Verify fork rule events (demo loop)" {
        Write-Host "  waiting for fork demo loop to emit rule decisions..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 10
        $deadline = (Get-Date).AddSeconds(30)
        $decisions = 0
        while ((Get-Date) -lt $deadline) {
            $demo = Invoke-Nl GET "/api/v1/demo/status"
            $decisions = [int]$demo.decisions
            if ($decisions -gt 0) {
                Write-Host ("  decisions={0}" -f $decisions) -ForegroundColor DarkGray
                return
            }
            Start-Sleep -Seconds 3
        }
        throw ("Expected rule decisions > 0 from fork demo loop; got {0}" -f $decisions)
    }
}

Step "Teardown (stop session)" {
    Invoke-Nl POST "/api/v1/session/stop" | Out-Null
    Start-Sleep -Seconds 2
    $status = Invoke-Nl GET "/api/v1/dogfood/status"
    if ($status.sessionRunning) {
        throw "Session still running after stop."
    }
}

Step "Verify fork destroyed after grace" {
    Write-Host ("  waiting up to {0}s for fork grace destroy (grace={1}s)..." -f $TeardownPollTimeoutSec, $TeardownGraceSec) -ForegroundColor DarkGray
    Start-Sleep -Seconds $TeardownGraceSec
    $deadline = (Get-Date).AddSeconds($TeardownPollTimeoutSec - $TeardownGraceSec)
    $count = -1
    while ((Get-Date) -lt $deadline) {
        $sessions = Invoke-Nl GET "/api/v1/fork/orchestrator/sessions"
        $count = @($sessions).Count
        if ($count -eq 0) {
            Write-Host "  fork sessions cleared" -ForegroundColor DarkGray
            return
        }
        Start-Sleep -Seconds 5
    }
    throw ("Expected 0 fork sessions, found {0}" -f $count)
}

Write-Host ""
Write-Host "DOGFOOD FLOW PASSED" -ForegroundColor Green
Write-Host "Manual replay: docs/NL_DOGFOOD_FLOW.md" -ForegroundColor DarkGray
