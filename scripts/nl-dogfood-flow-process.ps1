# Dogfood flow expecting a real NL.Fork.Runtime process (not mock).
# Terminal 1 must use NL_FORK_ORCHESTRATOR_MODE=process — see docs/NL_DOGFOOD_FLOW.md
param(
    [string]$BaseUrl = "http://127.0.0.1:27020"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $scriptDir "nl-dogfood-flow.ps1") -BaseUrl $BaseUrl -ExpectProvisioner process @args
