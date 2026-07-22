# Smoke-test NL Session Server (Phase D): admit + remote bridge
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$token = if ($env:NL_BUS_TOKEN) { $env:NL_BUS_TOKEN } else { "sess-" + [guid]::NewGuid().ToString("N") }
$opHeaders = @{}
if ($env:NL_OPERATOR_KEY) {
    $opHeaders["X-NL-Operator-Key"] = $env:NL_OPERATOR_KEY
}

Write-Host "Starting NL Session Server (token=$token) ..."
$env:NL_BUS_TOKEN = $token
$web = Start-Process -PassThru -NoNewWindow -FilePath "dotnet" -ArgumentList @(
    "run", "--project", (Join-Path $repo "src\NL.SessionHost.Web")
) -RedirectStandardOutput (Join-Path $env:TEMP "nl-sess-out.txt") `
  -RedirectStandardError (Join-Path $env:TEMP "nl-sess-err.txt")

Start-Sleep -Seconds 5

Write-Host "Fetching manifest ..."
$manifest = Invoke-RestMethod -Uri "http://127.0.0.1:27020/api/v1/session/manifest"
Write-Host "  bridge: $($manifest.bridgeConnectUrl)"
Write-Host "  admit:  $($manifest.admitUrl)"

Write-Host "Starting session with join gate ..."
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:27020/api/v1/session/bus-defaults" -Headers $opHeaders | Out-Null
$p = (Invoke-RestMethod -Uri "http://127.0.0.1:27020/api/v1/session").profile
$p | Add-Member -NotePropertyName joinGate -NotePropertyValue $true -Force
Invoke-RestMethod -Method Put -Uri "http://127.0.0.1:27020/api/v1/session/profile" `
  -ContentType "application/json" -Body ($p | ConvertTo-Json -Depth 6) -Headers $opHeaders | Out-Null
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:27020/api/v1/session/start" `
  -ContentType "application/json" -Body '{"replayOnce":false}' -Headers $opHeaders | Out-Null

Start-Sleep -Seconds 2

Write-Host "Banning Eve via moderation API ..."
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:27020/api/v1/moderation/profiles" `
  -ContentType "application/json" -Body '{"playerId":"Eve","displayName":"Eve"}' -Headers $opHeaders | Out-Null
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:27020/api/v1/moderation/ban" `
  -ContentType "application/json" -Body '{"streamerId":"default-streamer","playerId":"Eve","issuedBy":"smoke","reason":"test ban"}' -Headers $opHeaders | Out-Null

Write-Host "Testing admit API ..."
$alice = Invoke-RestMethod -Method Post -Uri $manifest.admitUrl `
  -ContentType "application/json" -Body '{"playerId":"Alice","displayName":"Alice"}'
$eve = Invoke-RestMethod -Method Post -Uri $manifest.admitUrl `
  -ContentType "application/json" -Body '{"playerId":"Eve","displayName":"Eve"}'
Write-Host "  Alice: admit=$($alice.admit) decision=$($alice.decision)"
Write-Host "  Eve:   admit=$($eve.admit) decision=$($eve.decision)"

$bridgeUrl = "ws://127.0.0.1:27021/nl/v1?token=$token"
Write-Host "Running Python bridge with admit ..."
python (Join-Path $repo "integrations\python\nl_bridge.py") `
  --url $bridgeUrl `
  --admit-url $manifest.admitUrl `
  --sample

Start-Sleep -Seconds 2
Stop-Process -Id $web.Id -Force -ErrorAction SilentlyContinue
Remove-Item Env:NL_BUS_TOKEN -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Session log (tail) ==="
try {
    $status = Invoke-RestMethod -Uri "http://127.0.0.1:27020/api/v1/session" -ErrorAction Stop
    $status.log | Select-Object -Last 10
} catch {
    Get-Content (Join-Path $env:TEMP "nl-sess-out.txt") -Tail 12 -ErrorAction SilentlyContinue
}
