# Stop native Windows NL demo started by deploy-demo-native.ps1
$ErrorActionPreference = "SilentlyContinue"
$repo = Split-Path $PSScriptRoot -Parent
$stateFile = Join-Path $repo "artifacts\demo-native\state.json"

if (-not (Test-Path $stateFile)) {
    Write-Host "No demo state file - nothing to stop."
    exit 0
}

$state = Get-Content $stateFile -Raw | ConvertFrom-Json
foreach ($name in @("serverPid", "bridgePid")) {
    $procId = $state.$name
    if ($procId) {
        Stop-Process -Id $procId -Force
        Write-Host "Stopped PID $procId ($name)"
    }
}

Remove-Item $stateFile -Force
Write-Host "Demo stopped."
