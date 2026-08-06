# Deploy NexoraLive to a remote VPS over SSH (from Windows)
param(
    [Parameter(Mandatory = $true)]
    [string]$VpsHost,
    [string]$VpsUser = "root",
    [string]$RepoPath = "/opt/NexoraLive",
    [string]$SshKey = "",
    [switch]$BootstrapOnly,
    [switch]$SkipValidate
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$ssh = @("ssh")
$scp = @("scp")
if ($SshKey) {
    $ssh += @("-i", $SshKey)
    $scp += @("-i", $SshKey)
}
$target = "{0}@{1}" -f $VpsUser, $VpsHost

Write-Host "=== NL VPS deploy from Windows ===" -ForegroundColor Cyan
Write-Host ("Host: {0}" -f $target)
Write-Host ("Repo: {0}" -f $RepoPath)

& ssh @ssh $target "command -v docker >/dev/null || (curl -fsSL https://get.docker.com | sh)"
if ($LASTEXITCODE -ne 0) { throw "SSH/docker setup failed" }

& ssh @ssh $target "test -d '$RepoPath' || git clone https://github.com/Surrplexie/NexoraLive.git '$RepoPath'"
if ($LASTEXITCODE -ne 0) { throw "git clone failed" }

& ssh @ssh $target "cd '$RepoPath' && git pull --ff-only"
if ($LASTEXITCODE -ne 0) { throw "git pull failed" }

if ($BootstrapOnly) {
    & ssh @ssh -t $target "cd '$RepoPath' && bash scripts/nl-vps-bootstrap.sh"
    if ($LASTEXITCODE -ne 0) { throw "bootstrap failed" }
    Write-Host "Bootstrap complete on VPS." -ForegroundColor Green
    exit 0
}

if (-not (Test-Path (Join-Path $Root "docker\.env.vps"))) {
    Write-Host "Missing docker\.env.vps locally — run on VPS: bash scripts/nl-vps-init-env.sh" -ForegroundColor Yellow
    Write-Host "Or copy your env files with scp before deploy." -ForegroundColor Yellow
    & ssh @ssh -t $target "cd '$RepoPath' && bash scripts/nl-vps-init-env.sh"
    if ($LASTEXITCODE -ne 0) { throw "init-env failed" }
} else {
    & scp @scp (Join-Path $Root "docker\.env.vps") "${target}:${RepoPath}/docker/.env.vps"
    if (Test-Path (Join-Path $Root "docker\vps-production-fleet.env")) {
        & scp @scp (Join-Path $Root "docker\vps-production-fleet.env") "${target}:${RepoPath}/docker/vps-production-fleet.env"
    }
}

& ssh @ssh $target "cd '$RepoPath' && bash scripts/nl-vps-deploy.sh"
if ($LASTEXITCODE -ne 0) { throw "deploy failed" }

$domain = (Select-String -Path (Join-Path $Root "docker\.env.vps") -Pattern '^NL_VPS_DOMAIN=(.+)$' -ErrorAction SilentlyContinue).Matches.Groups[1].Value
$op = (Select-String -Path (Join-Path $Root "docker\vps-production-fleet.env") -Pattern '^NL_OPERATOR_KEY=(.+)$' -ErrorAction SilentlyContinue).Matches.Groups[1].Value

if ($domain -and -not $SkipValidate) {
    Write-Host "Running remote validation..." -ForegroundColor Yellow
    & (Join-Path $Root "scripts/nl-vps-validate.ps1") -BaseUrl ("https://{0}" -f $domain.Trim()) -OperatorKey $op
}

Write-Host "VPS deploy complete." -ForegroundColor Green
