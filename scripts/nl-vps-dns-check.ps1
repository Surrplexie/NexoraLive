param(
    [Parameter(Mandatory = $true)]
    [string]$Domain,
    [string]$ExpectedIp = ""
)

$ErrorActionPreference = "Stop"

Write-Host ("Checking DNS for {0}..." -f $Domain) -ForegroundColor Cyan

try {
    $resolved = [System.Net.Dns]::GetHostAddresses($Domain) | ForEach-Object { $_.IPAddressToString }
} catch {
    throw ("DNS lookup failed for {0}: {1}" -f $Domain, $_.Exception.Message)
}

Write-Host ("Resolved: {0}" -f ($resolved -join ", ")) -ForegroundColor Green

if ($ExpectedIp) {
    if ($resolved -notcontains $ExpectedIp) {
        throw ("Expected IP {0} not in resolved set: {1}" -f $ExpectedIp, ($resolved -join ", "))
    }
    Write-Host ("OK: matches expected IP {0}" -f $ExpectedIp) -ForegroundColor Green
}

foreach ($sub in @("relay-us-east", "relay-us-west", "relay-eu-west")) {
    $parts = $Domain.Split(".", 2)
    if ($parts.Count -lt 2) { continue }
    $relay = "{0}.{1}" -f $sub, $parts[1]
    try {
        $r = [System.Net.Dns]::GetHostAddresses($relay) | ForEach-Object { $_.IPAddressToString }
        Write-Host ("OK: {0} -> {1}" -f $relay, ($r -join ", ")) -ForegroundColor Green
    } catch {
        Write-Host ("WARN: {0} not resolved yet" -f $relay) -ForegroundColor Yellow
    }
}

Write-Host "DNS check done." -ForegroundColor Cyan
