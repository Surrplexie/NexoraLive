# Register nlclient:// URL protocol on Windows (current user)
param(
    [string]$ClientExe = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if ([string]::IsNullOrWhiteSpace($ClientExe)) {
    $ClientExe = Join-Path $Root "dist\nl-client-win-x64\NL.Client.exe"
}

if (-not (Test-Path $ClientExe)) {
    throw "NL.Client.exe not found at $ClientExe — run build-nl-client-package.ps1 first"
}

$exePath = (Resolve-Path $ClientExe).Path
$classes = "HKCU:\Software\Classes\nlclient"

New-Item -Path $classes -Force | Out-Null
Set-ItemProperty -Path $classes -Name "(Default)" -Value "URL:NexoraLive Client Protocol"
Set-ItemProperty -Path $classes -Name "URL Protocol" -Value ""

New-Item -Path "$classes\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "$classes\shell\open\command" -Name "(Default)" -Value "`"$exePath`" deeplink --url `"%1`""

Write-Host "Registered nlclient:// -> $exePath" -ForegroundColor Green
