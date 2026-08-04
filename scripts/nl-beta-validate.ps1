# Phase 5 — public beta validation (waitlist + allowlist + join smoke)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [string]$OperatorKey = "",
    [string]$StreamerId = "dogfood-streamer",
    [string]$NlePath = "/app/samples/configs/fork-hello.nle"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    $envFile = Join-Path $Root "docker\beta-fleet.env"
    if (Test-Path $envFile) {
        foreach ($line in Get-Content $envFile) {
            if ($line -match '^NL_OPERATOR_KEY=(.+)$') {
                $OperatorKey = $Matches[1].Trim()
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($OperatorKey)) {
    throw "OperatorKey required (pass -OperatorKey or run nl-beta-stack-up.ps1 -Validate)"
}

function Invoke-NlApi {
    param([string]$Method, [string]$Path, $Body = $null, [switch]$Operator)
    $uri = ($BaseUrl.TrimEnd('/') + $Path)
    $headers = @{ "Content-Type" = "application/json" }
    if ($Operator) { $headers["X-NL-Operator-Key"] = $OperatorKey }
    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        ErrorAction = "Stop"
        TimeoutSec = 120
    }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Compress) }
    try {
        return Invoke-RestMethod @params
    } catch {
        throw $_.Exception.Message
    }
}

Write-Host "=== NL Phase 5 public beta validation ===" -ForegroundColor Cyan

Invoke-NlApi GET "/health" | Out-Null

$betaStatus = Invoke-NlApi GET "/api/v1/beta/status"
Write-Host ("Beta enabled: {0}  waitlist: {1}  slots: {2}/{3}" -f $betaStatus.enabled, $betaStatus.waitlistOpen, $betaStatus.approvedCount, $betaStatus.maxApprovedStreamers)

Write-Host "Testing waitlist signup..." -ForegroundColor Yellow
$contact = ("validate-{0}@nl.test" -f ([guid]::NewGuid().ToString('N').Substring(0, 8)))
$entry = Invoke-NlApi POST "/api/v1/beta/waitlist" @{
    displayName = "Beta Validate"
    contact = $contact
    twitchHandle = "nlvalidate"
    requestedGameId = "hello-fork"
}
Write-Host ("Waitlist entry: {0} ({1})" -f $entry.id, $entry.status) -ForegroundColor Green

Write-Host "Approving streamer via operator API..." -ForegroundColor Yellow
$approved = Invoke-NlApi POST ("/api/v1/beta/waitlist/{0}/approve" -f $entry.id) @{ streamerId = $StreamerId } -Operator
Write-Host ("Approved streamer: {0}" -f $approved.approvedStreamerId) -ForegroundColor Green

Write-Host "Verifying unapproved streamer is blocked..." -ForegroundColor Yellow
try {
    Invoke-NlApi POST "/api/v1/fork/orchestrator/create" @{
        streamerId = "not-approved-streamer"
        gameId = "hello-fork"
        majorVersion = "1.0"
        nlePath = $NlePath
        modIds = @()
        twitchFollowers = 100
    } | Out-Null
    throw "Expected fork create to fail for unapproved streamer"
} catch {
    Write-Host "OK: unapproved streamer blocked" -ForegroundColor Green
}

Write-Host "Running approved streamer dogfood smoke..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-dogfood-flow.ps1") -BaseUrl $BaseUrl -StreamerId $StreamerId -ExpectProvisioner docker -SkipImageBuild -OperatorKey $OperatorKey

Write-Host "Running beta validation gate..." -ForegroundColor Yellow
$report = Invoke-NlApi POST "/api/v1/beta/validation/run" @{} -Operator

Write-Host ""
Write-Host "Beta validation checks:" -ForegroundColor Cyan
foreach ($c in $report.checks) {
    $mark = if ($c.passed) { "PASS" } else { "FAIL" }
    $color = if ($c.passed) { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1}" -f $mark, $c.description) -ForegroundColor $color
    if ($c.detail) { Write-Host ("       {0}" -f $c.detail) -ForegroundColor DarkGray }
}

Write-Host ""
if ($report.betaPassed) {
    Write-Host "BETA VALIDATION PASSED" -ForegroundColor Green
    exit 0
}

Write-Host "BETA VALIDATION FAILED" -ForegroundColor Red
exit 1
