# Publishes Windows apps into artifacts/publish for a zip-friendly install layout.
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path $repo "artifacts\publish"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $out
New-Item -ItemType Directory -Force -Path $out | Out-Null

$apps = @(
    @{ Project = "src\NL.SessionHost\NL.SessionHost.csproj"; Name = "SessionHost" },
    @{ Project = "src\NL.SessionHost.Web\NL.SessionHost.Web.csproj"; Name = "SessionHostWeb" },
    @{ Project = "src\NL.Moderation.Web\NL.Moderation.Web.csproj"; Name = "ModerationWeb" },
    @{ Project = "src\NL.ModerationConsole\NL.ModerationConsole.csproj"; Name = "ModerationConsole" },
    @{ Project = "src\NL.ConfigEditor\NL.ConfigEditor.csproj"; Name = "ConfigEditor" },
    @{ Project = "src\NL.HotkeyDaemon\NL.HotkeyDaemon.csproj"; Name = "HotkeyDaemon" },
    @{ Project = "src\NL.Server\NL.Server.csproj"; Name = "Server" }
)

Push-Location $repo
try {
    foreach ($app in $apps) {
        $dest = Join-Path $out $app.Name
        Write-Host "Publishing $($app.Name) → $dest"
        dotnet publish $app.Project -c Release -o $dest --self-contained false
    }

    $hostDir = Join-Path $out "SessionHost"
    Write-Host ""
    Write-Host "Publish complete: $out"
    Write-Host "Run Session Host: $hostDir\NL.SessionHost.exe"
    Write-Host "Run Session Host (web): dotnet $((Join-Path $out 'SessionHostWeb\NL.SessionHost.Web.dll'))"
    Write-Host "Run Moderation (web): dotnet $((Join-Path $out 'ModerationWeb\NL.Moderation.Web.dll'))"
    Write-Host "Tools menu resolves ../ModerationConsole and ../ConfigEditor in this layout."
    Write-Host "Tip: zip the artifacts\publish directory for a simple portable install."
}
finally {
    Pop-Location
}
