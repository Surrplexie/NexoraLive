# NL Game Integration Spec v1 — log tail → NDJSON file

param(
    [Parameter(Mandatory = $true)][string]$LogPath,
    [Parameter(Mandatory = $true)][string]$OutPath,
    [string]$PlayerField = "player",
    [string]$EventPattern = '^\[(?<player>[^\]]+)\]\s+(?<msg>.+)$'
)

$utf8NoBom = New-Object System.Text.UTF8Encoding $false

if (-not (Test-Path $OutPath)) {
    [System.IO.File]::WriteAllText($OutPath, "# NL events from log tail`n", $utf8NoBom)
}

Get-Content -Path $LogPath -Wait -Tail 0 | ForEach-Object {
    $line = $_.TrimEnd()
    if ($line -eq "") { return }
    if ($line -match $EventPattern) {
        $player = $Matches.player
        $msg = $Matches.msg
        $eventName = if ($msg -match 'chat') { "playerChat" } else { "gameLog" }
        $ts = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
        $json = "{`"nl`":1,`"event`":`"$eventName`",`"player`":`"$player`",`"ts`":$ts,`"props`":{`"log.len`":$($msg.Length)}}"
        [System.IO.File]::AppendAllText($OutPath, "$json`n", $utf8NoBom)
        Write-Host $json
    }
}
