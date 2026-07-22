#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$composeFile = Join-Path $repo "docker\docker-compose.demo-local.yml"
$healthUrl = "http://127.0.0.1:27020/health"
$opsUrl = "http://127.0.0.1:27020/api/v1/ops/status"
$admitUrl = "http://127.0.0.1:27020/api/v1/session/admit"

$env:NL_BUS_TOKEN = if ($env:NL_BUS_TOKEN) { $env:NL_BUS_TOKEN } else { "hard-" + [guid]::NewGuid().ToString("N") }
$env:NL_OPERATOR_KEY = if ($env:NL_OPERATOR_KEY) { $env:NL_OPERATOR_KEY } else { "hard-op-" + [guid]::NewGuid().ToString("N") }
$env:NL_DEMO_RESET_INTERVAL_MINUTES = "0"
$env:NL_DEMO_BRIDGE_INTERVAL = "4"
$env:NL_HARDENING = "true"
if (-not $env:NL_ADMIT_RATE_PER_MIN) { $env:NL_ADMIT_RATE_PER_MIN = "5" }

function Cleanup {
    docker compose -f $composeFile down -v --remove-orphans 2>$null | Out-Null
}
try {
    Write-Host "=== NL Phase K hardening smoke ==="
    docker compose -f $composeFile up -d --build --wait

    Invoke-RestMethod -Uri $healthUrl | ConvertTo-Json -Compress
    $ops = Invoke-RestMethod -Uri $opsUrl
    if (-not $ops.hardening) { throw "Missing hardening in ops status." }

    $payload = '{"playerId":"RateTest","displayName":"RateTest"}'
    $got429 = $false
    for ($i = 0; $i -lt 20; $i++) {
        try {
            Invoke-RestMethod -Method Post -Uri $admitUrl -ContentType "application/json" -Body $payload | Out-Null
        } catch {
            if ($_.Exception.Response.StatusCode.value__ -eq 429) {
                $got429 = $true
                break
            }
        }
    }
    if (-not $got429) { throw "Expected 429 from admit rate limiter." }
    Write-Host "Phase K hardening smoke passed."
} finally {
    Cleanup
}
