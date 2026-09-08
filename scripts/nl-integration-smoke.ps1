# Smoke-test NL Integration Spec v1 (WebSocket + sample bridge)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

Write-Host "Starting NL.Server on ws://127.0.0.1:27021/nl/v1 ..."
$nl = Start-Process -PassThru -NoNewWindow -FilePath "dotnet" -ArgumentList @(
    "run", "--project", (Join-Path $repo "src\NL.Server"),
    "--", "--game", "generic",
    "--config", (Join-Path $repo "samples\configs\generic.nle"),
    "--source", "ws://127.0.0.1:27021/nl/v1",
    "--nl-action", "auto"
) -RedirectStandardOutput (Join-Path $env:TEMP "nl-smoke-out.txt") -RedirectStandardError (Join-Path $env:TEMP "nl-smoke-err.txt")

Start-Sleep -Seconds 4

Write-Host "Running Python reference bridge (--sample) ..."
python (Join-Path $repo "integrations\python\nl_bridge.py") --url ws://127.0.0.1:27021/nl/v1 --sample

Start-Sleep -Seconds 2
Stop-Process -Id $nl.Id -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== NL.Server output (tail) ==="
Get-Content (Join-Path $env:TEMP "nl-smoke-out.txt") -Tail 15 -ErrorAction SilentlyContinue
Write-Host "=== stderr ==="
Get-Content (Join-Path $env:TEMP "nl-smoke-err.txt") -Tail 10 -ErrorAction SilentlyContinue
