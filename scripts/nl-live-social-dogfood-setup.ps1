# Live social dogfood — install fixtures under NL_DATA_ROOT / NL_SOCIAL_ROOT
param(
    [ValidateSet("mock", "live")]
    [string]$SocialMode = "mock",
    [string]$DataRoot = "",
    [string]$SocialRoot = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($DataRoot)) {
    $DataRoot = Join-Path $env:LOCALAPPDATA "NL"
}
if ([string]::IsNullOrWhiteSpace($SocialRoot)) {
    $SocialRoot = Join-Path $DataRoot "social"
}

$env:NL_DATA_ROOT = $DataRoot
$env:NL_SOCIAL_ROOT = $SocialRoot
$env:NL_SOCIAL_ENABLED = "true"
$env:NL_SOCIAL_MODE = $SocialMode

Write-Host "=== NL live social dogfood setup ===" -ForegroundColor Cyan
Write-Host ("Data root:   {0}" -f $DataRoot) -ForegroundColor DarkGray
Write-Host ("Social root: {0}" -f $SocialRoot) -ForegroundColor DarkGray
Write-Host ("Social mode: {0}" -f $SocialMode) -ForegroundColor DarkGray

New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
New-Item -ItemType Directory -Force -Path $SocialRoot | Out-Null

$joinReqSrc = Join-Path $Root "samples\social\dogfood-join-requirements.json"
$mockSrc = Join-Path $Root "samples\social\dogfood-mock-social.json"
$streamerSrc = Join-Path $Root "samples\social\dogfood-streamer-social.json"

foreach ($src in @($joinReqSrc, $mockSrc, $streamerSrc)) {
    if (-not (Test-Path $src)) { throw ("Sample missing: {0}" -f $src) }
}

Copy-Item $joinReqSrc (Join-Path $DataRoot "join-requirements.json") -Force
Copy-Item $mockSrc (Join-Path $SocialRoot "mock-social.json") -Force

$streamerJson = Get-Content $streamerSrc -Raw
if ($env:DOGFOOD_TWITCH_BROADCASTER_ID) {
    $streamerJson = $streamerJson.Replace("123456789", $env:DOGFOOD_TWITCH_BROADCASTER_ID)
}
if ($env:DOGFOOD_DISCORD_GUILD_ID) {
    $streamerJson = $streamerJson.Replace("987654321", $env:DOGFOOD_DISCORD_GUILD_ID)
}
if ($env:DOGFOOD_KICK_SLUG) {
    $streamerJson = $streamerJson.Replace('"dogfood"', ('"' + $env:DOGFOOD_KICK_SLUG + '"'))
}
Set-Content -Path (Join-Path $SocialRoot "streamer-social.json") -Value $streamerJson -Encoding UTF8

Write-Host "OK: join-requirements.json" -ForegroundColor Green
Write-Host "OK: mock-social.json (live status + mock relationships)" -ForegroundColor Green
Write-Host "OK: streamer-social.json" -ForegroundColor Green

if ($SocialMode -eq "live") {
    $required = @(
        @{ Name = "TWITCH_CLIENT_ID"; Hint = "Twitch developer app client id" },
        @{ Name = "TWITCH_CLIENT_SECRET"; Hint = "Twitch developer app secret" }
    )
    $missing = @()
    foreach ($r in $required) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($r.Name))) {
            $missing += $r
        }
    }
    if ($missing.Count -gt 0) {
        Write-Host ""
        Write-Host "Live mode needs OAuth credentials before platform checks work:" -ForegroundColor Yellow
        foreach ($m in $missing) {
            Write-Host ("  {0} - {1}" -f $m.Name, $m.Hint) -ForegroundColor DarkYellow
        }
        Write-Host "Copy samples/social/live-social-dogfood.env.example and fill values." -ForegroundColor DarkGray
    } else {
        Write-Host "OK: Twitch OAuth env present" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Next:" -ForegroundColor Cyan
Write-Host "  1. powershell -File scripts/nl-session-host-live-social-dogfood.ps1 -SocialMode $SocialMode"
Write-Host "  2. Open http://127.0.0.1:27020/live-social-dogfood.html"
Write-Host "  3. powershell -File scripts/nl-live-social-dogfood-flow.ps1 -SocialMode $SocialMode"
Write-Host ""
Write-Host "LIVE SOCIAL DOGFOOD SETUP COMPLETE" -ForegroundColor Green
exit 0
