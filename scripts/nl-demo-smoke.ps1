#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$composeFile = Join-Path $repo "docker\docker-compose.demo-local.yml"
$healthUrl = if ($env:NL_DEMO_SMOKE_HEALTH_URL) { $env:NL_DEMO_SMOKE_HEALTH_URL } else { "http://127.0.0.1:27020/health" }
$demoUrl = if ($env:NL_DEMO_SMOKE_DEMO_URL) { $env:NL_DEMO_SMOKE_DEMO_URL } else { "http://127.0.0.1:27020/api/v1/demo/status" }

$env:NL_BUS_TOKEN = if ($env:NL_BUS_TOKEN) { $env:NL_BUS_TOKEN } else { "demo-" + [guid]::NewGuid().ToString("N") }
$env:NL_OPERATOR_KEY = if ($env:NL_OPERATOR_KEY) { $env:NL_OPERATOR_KEY } else { "demo-op-" + [guid]::NewGuid().ToString("N") }
if (-not $env:NL_DEMO_RESET_INTERVAL_MINUTES) { $env:NL_DEMO_RESET_INTERVAL_MINUTES = "0" }
if (-not $env:NL_DEMO_BRIDGE_INTERVAL) { $env:NL_DEMO_BRIDGE_INTERVAL = "3" }

function Cleanup {
    docker compose -f $composeFile down -v --remove-orphans 2>$null | Out-Null
}
try {
    Write-Host "=== NL Phase G demo smoke ==="
    docker compose -f $composeFile up -d --build --wait

    $running = $false
    for ($i = 0; $i -lt 45; $i++) {
        try {
            $demo = Invoke-RestMethod -Uri $demoUrl -ErrorAction Stop
            if ($demo.sessionRunning) {
                $running = $true
                break
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
    if (-not $running) {
        throw "Demo session did not start."
    }

    for ($i = 0; $i -lt 40; $i++) {
        $demo = Invoke-RestMethod -Uri $demoUrl
        if ($demo.decisions -gt 0) {
            Write-Host "Decisions recorded: $($demo.decisions)"
            Invoke-RestMethod -Uri $healthUrl | ConvertTo-Json -Compress
            $demo | ConvertTo-Json -Compress
            exit 0
        }
        Start-Sleep -Seconds 2
    }

    throw "No decisions recorded - demo bridge may not be connected."
} finally {
    Cleanup
}
