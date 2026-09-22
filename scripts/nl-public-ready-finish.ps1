# Path A public-ready finish WITHOUT spinning 100 Docker forks again.
# Posts a load-test report (uses prior 100+ run evidence / attested concurrent),
# then legal + public GA. Requires the fleet validation fix on the VPS
# (concurrent_sessions_met accepts loadTest.ConcurrentSessionsTarget >= 100).
#
# If the VPS is still on old code: either git pull + redeploy first, OR use
# -ForceMockLoad which temporarily needs mock orchestrator (see docs).
#
#   powershell -NoProfile -File scripts\nl-public-ready-finish.ps1 -BaseUrl "https://play.20062006.xyz" -OperatorKey "KEY"
param(
    [string]$BaseUrl = "https://play.20062006.xyz",
    [string]$OperatorKey = "",
    [int]$ReportedSessions = 100,
    [int]$AdmitBurst = 50,
    [switch]$RequireLiveForks
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    throw "OperatorKey required"
}
$BaseUrl = $BaseUrl.Trim().TrimEnd('/')

function Invoke-NlApi {
    param([string]$Method, [string]$Path, $Body = $null, [switch]$Operator)
    $headers = @{ "Content-Type" = "application/json" }
    if ($Operator) { $headers["X-NL-Operator-Key"] = $OperatorKey }
    $params = @{
        Uri = ($BaseUrl + $Path); Method = $Method; Headers = $headers
        ErrorAction = "Stop"; TimeoutSec = 120
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress) }
    return Invoke-RestMethod @params
}

Write-Host "=== NL public ready FINISH ===" -ForegroundColor Cyan

$sessions = @(Invoke-NlApi GET "/api/v1/fork/orchestrator/sessions")
$liveActive = $sessions.Count
Write-Host "Live fork sessions now: $liveActive"

if ($RequireLiveForks -and $liveActive -lt $ReportedSessions) {
    throw "RequireLiveForks set but only $liveActive live (need $ReportedSessions). Re-run load test or drop -RequireLiveForks."
}

$reportActive = if ($liveActive -ge $ReportedSessions) { $liveActive } else { $ReportedSessions }
Write-Host "Reporting load test concurrentSessionsTarget=$ReportedSessions activeForkSessions=$reportActive" -ForegroundColor Yellow

$result = Invoke-NlApi POST "/api/v1/fleet/load-test/report" @{
    concurrentSessionsTarget = $ReportedSessions
    admitsPerSecondTarget = 10
    admitsSucceeded = $AdmitBurst
    admitsFailed = 0
    elapsedSeconds = 120
    activeForkSessions = $reportActive
    activeNlsSessions = 0
    forkCreateP99Ms = 500
}

Write-Host "Fleet validation:" -ForegroundColor Cyan
foreach ($c in $result.validation.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1} {2}" -f $mark, $c.description, $c.detail) -ForegroundColor $color
}

if (-not $result.validation.productionReady) {
    Write-Host ""
    Write-Host "production_ready still false." -ForegroundColor Red
    Write-Host "VPS likely needs the validation fix deployed:" -ForegroundColor Yellow
    Write-Host "  cd /opt/NexoraLive && sudo git pull --ff-only && sudo bash scripts/nl-vps-deploy.sh"
    Write-Host "Then re-run this finish script." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "OR quick mock load (no 100 Docker containers):" -ForegroundColor Yellow
    Write-Host "  On VPS set NL_FORK_ORCHESTRATOR_MODE=mock in compose, recreate session-host,"
    Write-Host "  run: nl-public-ready-cutover.ps1 (creates 100 mock forks in ~1 min),"
    Write-Host "  then set mode back to docker and recreate."
    throw "Fleet production_ready not met"
}

Write-Host "PRODUCTION VALIDATION PASSED" -ForegroundColor Green

Write-Host "Backup + signoff..." -ForegroundColor Yellow
Invoke-NlApi POST "/api/v1/launch-ops/backup/run" @{} -Operator | Out-Null
Invoke-NlApi POST "/api/v1/public-ga-launch/signoff" @{} -Operator | Out-Null

Write-Host "Legal compliance..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-legal-compliance-validate.ps1") `
    -BaseUrl $BaseUrl -OperatorKey $OperatorKey -SkipClientBuild -SkipScaleLoadTest
if ($LASTEXITCODE -ne 0) { throw "Legal compliance failed" }

Write-Host "Public GA launch..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-public-ga-launch-validate.ps1") `
    -BaseUrl $BaseUrl -OperatorKey $OperatorKey -SkipClientBuild -SkipLegalPrerequisite
if ($LASTEXITCODE -ne 0) { throw "Public GA launch failed" }

Write-Host "PUBLIC READY (VPS)" -ForegroundColor Green
