param([int]$Port = 8765)
$ips = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
    ForEach-Object { $_.IPAddress } | Sort-Object -Unique)
if ($ips.Count -eq 0) {
    Write-Host '[GameServer]   LAN: (not detected, see Python log after start)'
} else {
    foreach ($ip in $ips) {
        Write-Host "[GameServer]   LAN: http://${ip}:$Port"
    }
}
