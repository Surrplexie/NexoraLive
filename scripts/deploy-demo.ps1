# Deploy NL public demo stack (Phase F) on Windows.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$composeFile = Join-Path $repo "docker\docker-compose.demo.yml"
$envFile = Join-Path $repo "docker\.env"

if (-not (Test-Path $envFile)) {
    Write-Error "Missing docker\.env — copy docker\.env.demo.example to docker\.env and set secrets."
}

Get-Content $envFile | ForEach-Object {
    if ($_ -match '^\s*([^#=]+)=(.*)$') {
        $name = $matches[1].Trim()
        $value = $matches[2].Trim().Trim('"')
        Set-Item -Path "Env:$name" -Value $value
    }
}

$domain = if ($env:NL_DEMO_DOMAIN) { $env:NL_DEMO_DOMAIN } else { "localhost" }
if ($domain -in @("localhost", "127.0.0.1")) {
    $env:NL_PUBLIC_HTTP = if ($env:NL_PUBLIC_HTTP) { $env:NL_PUBLIC_HTTP } else { "http://localhost" }
    $env:NL_PUBLIC_WS = if ($env:NL_PUBLIC_WS) { $env:NL_PUBLIC_WS } else { "ws://localhost/nl/v1" }
    $env:NL_PUBLIC_MOD_HTTP = if ($env:NL_PUBLIC_MOD_HTTP) { $env:NL_PUBLIC_MOD_HTTP } else { "http://localhost" }
    $env:NL_CORS_ORIGINS = if ($env:NL_CORS_ORIGINS) { $env:NL_CORS_ORIGINS } else { "http://localhost" }
    $healthUrl = "http://localhost/health"
} else {
    $env:NL_PUBLIC_HTTP = if ($env:NL_PUBLIC_HTTP) { $env:NL_PUBLIC_HTTP } else { "https://$domain" }
    $env:NL_PUBLIC_WS = if ($env:NL_PUBLIC_WS) { $env:NL_PUBLIC_WS } else { "wss://$domain/nl/v1" }
    $env:NL_PUBLIC_MOD_HTTP = if ($env:NL_PUBLIC_MOD_HTTP) { $env:NL_PUBLIC_MOD_HTTP } else { "https://$domain" }
    $env:NL_CORS_ORIGINS = if ($env:NL_CORS_ORIGINS) { $env:NL_CORS_ORIGINS } else { "https://$domain" }
    $healthUrl = "https://$domain/health"
}

if (-not $env:NL_BUS_TOKEN -or $env:NL_BUS_TOKEN -like "replace-*") {
    Write-Error "Set NL_BUS_TOKEN in docker\.env"
}
if (-not $env:NL_OPERATOR_KEY -or $env:NL_OPERATOR_KEY -like "replace-*") {
    Write-Error "Set NL_OPERATOR_KEY in docker\.env"
}

Write-Host "=== NL demo deploy ==="
Write-Host "Domain:      $domain"
Write-Host "Public HTTP: $($env:NL_PUBLIC_HTTP)"

docker compose -f $composeFile --env-file $envFile up -d --build --wait

for ($i = 0; $i -lt 45; $i++) {
    try {
        $resp = Invoke-RestMethod -Uri $healthUrl -SkipCertificateCheck -ErrorAction Stop
        Write-Host "Demo is up: $healthUrl"
        $resp | ConvertTo-Json -Compress
        Write-Host "Dashboard: $($env:NL_PUBLIC_HTTP)/"
        exit 0
    } catch {
        Start-Sleep -Seconds 2
    }
}

Write-Error "Edge health check failed: $healthUrl"
