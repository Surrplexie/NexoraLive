# Seeds %LOCALAPPDATA%\NL with Eve banned for default-streamer, then optionally runs join-gate replay.
$ErrorActionPreference = "Stop"
$root = Join-Path $env:LOCALAPPDATA "NL"
New-Item -ItemType Directory -Force -Path $root | Out-Null

$profilesPath = Join-Path $root "sp-profiles.json"
$now = [DateTimeOffset]::UtcNow.ToString("o")
$json = @"
[
  {
    "Id": "Eve",
    "DisplayName": "Eve",
    "AccountCreatedAtUtc": "$now",
    "Verification": "None",
    "Offenses": [
      {
        "StreamerId": "default-streamer",
        "IssuedAtUtc": "$now",
        "IssuedBy": "seed-script",
        "Reason": "seed ban for join-gate demo",
        "Game": "generic",
        "PriorContext": null
      }
    ],
    "Relationships": [
      {
        "StreamerId": "default-streamer",
        "Standing": "Banned",
        "IsFollowing": false,
        "IsSubscribed": false,
        "Roles": []
      }
    ]
  }
]
"@
Set-Content -Path $profilesPath -Value $json -Encoding UTF8
Write-Host "Wrote banned Eve profile → $profilesPath"

$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    dotnet run --project src/NL.Server -- --game generic `
        --config samples/configs/generic.nle `
        --source samples/events/join-gate-sample.ndjson `
        --replay --join-gate `
        --streamer default-streamer `
        --sp-store $profilesPath `
        --moderation-log (Join-Path $root "moderation.jsonl")
}
finally {
    Pop-Location
}
