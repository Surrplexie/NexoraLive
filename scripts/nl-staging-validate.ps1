# Phase 3 — run fleet staging validation against the compose stack (100+ mock forks)
param(
    [string]$BaseUrl = "http://127.0.0.1:27020",
    [int]$ConcurrentSessions = 100,
    [int]$AdmitBurst = 50,
    [string]$NlePath = "/app/samples/configs/fork-hello.nle",
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$params = @{
    BaseUrl             = $BaseUrl
    ConcurrentSessions  = $ConcurrentSessions
    AdmitBurst          = $AdmitBurst
    NlePath             = $NlePath
}
if ($SkipCleanup) { $params.SkipCleanup = $true }

& (Join-Path $scriptDir "nl-fleet-staging-validation.ps1") @params
