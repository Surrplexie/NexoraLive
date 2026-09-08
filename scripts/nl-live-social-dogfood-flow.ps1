# End-to-end live social dogfood: social setup, start, follower join, stranger denied, teardown
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [ValidateSet("mock", "live")]
    [string]$SocialMode = "mock",
    [string]$StreamerId = "dogfood-streamer",
    [string]$FollowerPlayerId = "sp-dogfood-1",
    [string]$StrangerPlayerId = "sp-dogfood-stranger",
    [string]$Steam64 = "76561198000000001",
    [string]$GameId = "hello-fork",
    [string]$OperatorKey = "",
    [switch]$SkipImageBuild
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
    if (-not [string]::IsNullOrWhiteSpace($OperatorKey)) {
        $params.Headers = @{ "X-NL-Operator-Key" = $OperatorKey }
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

Write-Host "=== NL live social dogfood flow ===" -ForegroundColor Cyan
Write-Host ("Social mode: {0}" -f $SocialMode) -ForegroundColor DarkGray
Write-Host ("Follower: {0} | Stranger: {1}" -f $FollowerPlayerId, $StrangerPlayerId) -ForegroundColor DarkGray

if (-not $SkipImageBuild) {
    $imgKey = $GameId.Trim().ToLowerInvariant()
    $dockerfile = switch ($imgKey) {
        "rimworld" { "docker/fork-rimworld/Dockerfile"; break }
        "kenshi" { "docker/fork-kenshi/Dockerfile"; break }
        "minecraft" { "docker/fork-minecraft/Dockerfile"; break }
        default { "docker/fork-hello/Dockerfile" }
    }
    $tag = switch ($imgKey) {
        "rimworld" { "nl-fork-rimworld:latest"; break }
        "kenshi" { "nl-fork-kenshi:latest"; break }
        "minecraft" { "nl-fork-minecraft:latest"; break }
        default { "nl-fork-hello:latest" }
    }
    Write-Host ("Building {0} ..." -f $tag) -ForegroundColor DarkGray
    docker build -f $dockerfile -t $tag .
    if ($LASTEXITCODE -ne 0) { throw "docker build failed" }
}

Invoke-Nl GET "/health" | Out-Null
Write-Host "OK: health" -ForegroundColor Green

try { Invoke-Nl POST "/api/v1/session/stop" | Out-Null } catch { }
Start-Sleep -Seconds 1

$setupRaw = Invoke-Nl POST "/api/v1/dogfood/setup" @{ gameId = $GameId; socialMode = $SocialMode }
$setup = if ($setupRaw.status) { $setupRaw.status } else { $setupRaw }
if (-not $setup.profile.socialGateEnabled) {
    throw "Expected socialGateEnabled=true on dogfood profile."
}
if (-not $setup.profile.joinGate) {
    throw "Expected joinGate=true on dogfood profile."
}
Write-Host "OK: dogfood setup with social gate" -ForegroundColor Green

Invoke-Nl POST "/api/v1/session/start" @{ replayOnce = $false } | Out-Null
Start-Sleep -Seconds 2
$status = Invoke-Nl GET "/api/v1/dogfood/status"
if (-not $status.sessionRunning) { throw "Session not running after start." }
Write-Host "OK: session started (live-only gate satisfied via mock live fixture)" -ForegroundColor Green

$join = Invoke-Nl POST "/api/v1/client/join-flow" @{
    playerId = $FollowerPlayerId
    streamerId = $StreamerId
    platformUserId = $Steam64
    platform = "steam"
    gameId = $GameId
    majorVersion = "1.0"
    atOwnRiskAcknowledged = $true
    mode = "Player"
}
if (-not $join.success) {
    throw ("Follower join failed at step {0}: {1}" -f $join.step, $join.message)
}
Write-Host "OK: follower join flow completed" -ForegroundColor Green

$strangerAdmit = Invoke-Nl POST "/api/v1/session/admit" @{
    playerId = $StrangerPlayerId
    streamerId = $StreamerId
    displayName = "Stranger"
    platformUserId = $Steam64
    platform = "steam"
    gameId = $GameId
    majorVersion = "1.0"
    atOwnRiskAcknowledged = $true
}
if ($strangerAdmit.admit -eq $true) {
    throw "Expected stranger admit denied by social/join gate."
}
$denyReason = $strangerAdmit.reason
if (-not $denyReason) { $denyReason = $strangerAdmit.decision }
Write-Host ("OK: stranger denied ({0})" -f $denyReason) -ForegroundColor Green

Invoke-Nl POST "/api/v1/session/stop" | Out-Null
Write-Host "OK: teardown" -ForegroundColor Green

Write-Host ""
Write-Host "LIVE SOCIAL DOGFOOD FLOW PASSED" -ForegroundColor Green
exit 0
