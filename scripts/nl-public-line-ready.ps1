# Public-line rehearsal: build fork images, dogfood RimWorld path, GA pages.
# Does not buy a VPS. Prints operator leftovers at the end.
param(
    [switch]$SkipBuild,
    [switch]$SkipForkImages,
    [switch]$SkipClientPackage,
    [switch]$SkipTests,
    [switch]$SkipStack
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $Root

Write-Host "=== NL public line rehearsal ===" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "Unit tests (dogfood + GA launch)..." -ForegroundColor Yellow
    dotnet test tests/NL.Fleet.Tests/NL.Fleet.Tests.csproj --filter "FullyQualifiedName~ProductionDogfood|FullyQualifiedName~PublicGaLaunch"
    if ($LASTEXITCODE -ne 0) { throw "Fleet unit tests failed" }
}

$docker = Get-Command docker -ErrorAction SilentlyContinue
if (-not $docker) {
    Write-Host "Docker CLI not found - code checks only." -ForegroundColor Yellow
    Write-Host "PUBLIC LINE CODE READY (install Docker Desktop to rehearse forks)" -ForegroundColor Green
    exit 0
}

$dockerOk = $false
try {
    docker info 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { $dockerOk = $true }
} catch {
    $dockerOk = $false
}
if (-not $dockerOk) {
    Write-Host "Docker daemon not running - code checks only." -ForegroundColor Yellow
    Write-Host "PUBLIC LINE CODE READY (start Docker Desktop to rehearse forks)" -ForegroundColor Green
    exit 0
}

if ($SkipStack) {
    Write-Host "SkipStack set - not starting compose." -ForegroundColor Yellow
    Write-Host "PUBLIC LINE CODE READY" -ForegroundColor Green
    exit 0
}

Write-Host "Starting production-dogfood stack (local rehearsal)..." -ForegroundColor Yellow
$up = Join-Path $Root "scripts/nl-production-dogfood-stack-up.ps1"
$upArgs = @{}
if ($SkipBuild) { $upArgs["SkipBuild"] = $true }
if ($SkipForkImages) { $upArgs["SkipForkImages"] = $true }
if ($SkipClientPackage) { $upArgs["SkipClientPackage"] = $true }
& $up @upArgs
if ($LASTEXITCODE -ne 0) { throw "dogfood stack-up failed" }

$envFile = Join-Path $Root "docker\production-dogfood-fleet.env"
$operatorKey = ""
foreach ($line in Get-Content $envFile) {
    if ($line -match '^NL_OPERATOR_KEY=(.+)$') {
        $operatorKey = $Matches[1].Trim()
        break
    }
}
if ([string]::IsNullOrWhiteSpace($operatorKey)) {
    throw "NL_OPERATOR_KEY missing from docker/production-dogfood-fleet.env"
}

Write-Host "Public-line dogfood (hello-fork + minecraft + rimworld)..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-production-dogfood-validate.ps1") `
    -OperatorKey $operatorKey `
    -SkipClientBuild `
    -PublicLine
if ($LASTEXITCODE -ne 0) { throw "Public-line dogfood failed" }

Write-Host "Public GA launch gate..." -ForegroundColor Yellow
& (Join-Path $Root "scripts/nl-public-ga-launch-validate.ps1") `
    -OperatorKey $operatorKey `
    -SkipClientBuild `
    -SkipLegalPrerequisite
if ($LASTEXITCODE -ne 0) { throw "Public GA launch validation failed" }

Write-Host ""
Write-Host "PUBLIC LINE READY (local)" -ForegroundColor Green
Write-Host "Play:     http://127.0.0.1:27020/play.html"
Write-Host "Signup:   http://127.0.0.1:27020/ga.html"
Write-Host "RimWorld: catalog rimworld@1.0"
Write-Host ""
Write-Host "Operator leftovers (not in git):" -ForegroundColor Yellow
Write-Host "  1. Push this tree to GitHub so docker/ is in the clone"
Write-Host "  2. Ubuntu VPS 4GB+ and DNS for play. plus relay hosts"
Write-Host "  3. bash scripts/nl-vps-bootstrap.sh on the VPS"
Write-Host "  4. STEAM_WEB_API_KEY in docker/vps-production-fleet.env"
Write-Host "  5. powershell -File scripts/nl-vps-validate.ps1 -BaseUrl https://play.YOURDOMAIN -OperatorKey YOUR_KEY"
Write-Host "Docs: docs/NL_PUBLIC_LINE.md"
exit 0
