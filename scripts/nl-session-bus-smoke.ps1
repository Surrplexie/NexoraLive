# Smoke-test NL Session Bus (web host + authenticated bridge)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$token = "smoke-" + [guid]::NewGuid().ToString("N")

Write-Host "Starting NL.SessionHost.Web (token=$token) ..."
$env:NL_BUS_TOKEN = $token
$web = Start-Process -PassThru -NoNewWindow -FilePath "dotnet" -ArgumentList @(
    "run", "--project", (Join-Path $repo "src\NL.SessionHost.Web")
) -RedirectStandardOutput (Join-Path $env:TEMP "nl-bus-out.txt") `
  -RedirectStandardError (Join-Path $env:TEMP "nl-bus-err.txt")

Start-Sleep -Seconds 5

Write-Host "Applying bus defaults and starting session ..."
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:27020/api/v1/session/bus-defaults" | Out-Null
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:27020/api/v1/session/start" `
  -ContentType "application/json" -Body '{"replayOnce":false}' | Out-Null

Start-Sleep -Seconds 2

$bridgeUrl = "ws://127.0.0.1:27021/nl/v1?token=$token"
Write-Host "Running Python bridge → $bridgeUrl"
python (Join-Path $repo "integrations\python\nl_bridge.py") --url $bridgeUrl --sample

Start-Sleep -Seconds 2
Stop-Process -Id $web.Id -Force -ErrorAction SilentlyContinue
Remove-Item Env:NL_BUS_TOKEN -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Session status (tail log) ==="
try {
    $status = Invoke-RestMethod -Uri "http://127.0.0.1:27020/api/v1/session" -ErrorAction Stop
    $status.log | Select-Object -Last 8
} catch {
    Get-Content (Join-Path $env:TEMP "nl-bus-out.txt") -Tail 12 -ErrorAction SilentlyContinue
}
