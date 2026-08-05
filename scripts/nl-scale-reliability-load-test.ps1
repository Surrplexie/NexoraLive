# Phase 12 — GA traffic load test (128 concurrent mock fork sessions + production SLO report)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [int]$ConcurrentSessions = 128,
    [int]$AdmitBurst = 50,
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

& (Join-Path $Root "scripts/nl-fleet-staging-validation.ps1") `
    -BaseUrl $BaseUrl `
    -ConcurrentSessions $ConcurrentSessions `
    -AdmitBurst $AdmitBurst `
    -NlePath "/app/samples/configs/fork-hello.nle" `
    -SkipCleanup:$SkipCleanup

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Phase 12 GA load test complete ($ConcurrentSessions sessions)" -ForegroundColor Green
exit 0
