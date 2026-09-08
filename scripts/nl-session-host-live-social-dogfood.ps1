# Terminal 1 — Session Host for live social dogfood (Phase M + dogfood fork)
param(
    [ValidateSet("mock", "live")]
    [string]$SocialMode = "mock",
    [ValidateSet("mock", "live")]
    [string]$OwnershipMode = "mock",
    [string]$EnvFile = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if (-not [string]::IsNullOrWhiteSpace($EnvFile)) {
    if (-not (Test-Path $EnvFile)) { throw "Env file not found: $EnvFile" }
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
            Set-Item -Path "Env:$($Matches[1])" -Value $Matches[2].Trim('"')
        }
    }
    Write-Host ("Loaded env: {0}" -f $EnvFile) -ForegroundColor DarkGray
}

& (Join-Path $Root "scripts/nl-live-social-dogfood-setup.ps1") -SocialMode $SocialMode `
    -DataRoot ($env:NL_DATA_ROOT) -SocialRoot ($env:NL_SOCIAL_ROOT)

$env:NL_FLEET_ENABLED = "true"
$env:NL_FLEET_MIN_TWITCH_FOLLOWERS = "0"
$env:NL_FLEET_MAX_FORK_CREATES_PER_HOUR = "999"
$env:NL_FORK_ORCHESTRATOR_ENABLED = "true"
$env:NL_FORK_ORCHESTRATOR_MODE = "docker"
$env:NL_IDENTITY_ENABLED = "true"
$env:NL_OWNERSHIP_MODE = $OwnershipMode
$env:NL_PUBLIC_BASE_URL = if ($env:NL_PUBLIC_BASE_URL) { $env:NL_PUBLIC_BASE_URL } else { "http://127.0.0.1:27020" }
$env:NL_SOCIAL_ENABLED = "true"
$env:NL_SOCIAL_MODE = $SocialMode

if ($OwnershipMode -eq "live" -and -not $env:STEAM_WEB_API_KEY) {
    Write-Host "Set STEAM_WEB_API_KEY before -OwnershipMode live" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host ("Starting Session Host (live social dogfood, social={0}, ownership={1})..." -f $SocialMode, $OwnershipMode) -ForegroundColor Cyan
Write-Host "UI: http://127.0.0.1:27020/live-social-dogfood.html" -ForegroundColor Green

if ($NoBuild) {
    dotnet run --project src/NL.SessionHost.Web --no-build
} else {
    dotnet run --project src/NL.SessionHost.Web
}
