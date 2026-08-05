# Build NL Client Windows package for Phase 11 distribution
param(
    [string]$Version = "",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Date -Format "yyyy.MM.dd.HHmm")
}

$outDir = Join-Path $Root "dist\nl-client-$Runtime"
$zipPath = Join-Path $Root "src\NL.SessionHost.Web\wwwroot\downloads\nl-client-$Runtime.zip"
$manifestPath = Join-Path $Root "src\NL.SessionHost.Web\wwwroot\downloads\nl-client-manifest.json"
$downloadsDir = Split-Path $zipPath -Parent

New-Item -ItemType Directory -Force -Path $downloadsDir | Out-Null
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

Write-Host "Publishing NL.Client ($Runtime)..." -ForegroundColor Cyan
dotnet publish (Join-Path $Root "src\NL.Client\NL.Client.csproj") -c Release -r $Runtime --self-contained false -o $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

@(
    "# NL Client $Version",
    "Run: NL.Client.exe join --help",
    "Deep link: nlclient://join?streamer=...&game=hello-fork&major=1.0"
) | Set-Content -Path (Join-Path $outDir "README.txt") -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -Force

$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
@{
    version = $Version
    platform = $Runtime
    sha256 = $hash
    builtAtUtc = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json | Set-Content -Path $manifestPath -Encoding UTF8

Write-Host ("OK: {0} ({1} bytes, sha256={2})" -f $zipPath, (Get-Item $zipPath).Length, $hash.Substring(0, 16) + "...") -ForegroundColor Green
