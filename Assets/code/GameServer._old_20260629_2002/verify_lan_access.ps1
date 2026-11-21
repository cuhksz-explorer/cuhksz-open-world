# Run on the machine hosting GameServer. Checks whether LAN teammates can connect.
$ErrorActionPreference = 'Continue'
$port = if ($env:GAME_SERVER_PORT) { [int]$env:GAME_SERVER_PORT } else { 8765 }
$expectedIp = '192.168.1.113'

Write-Host "=== GameServer LAN check (port $port) ===" -ForegroundColor Cyan

$listening = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalAddress -eq '0.0.0.0' -or $_.LocalAddress -eq '::' }
if ($listening) {
    Write-Host '(OK) Port is listening on 0.0.0.0' -ForegroundColor Green
} else {
    $anyListen = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($anyListen) {
        $addrs = ($anyListen | ForEach-Object { $_.LocalAddress }) -join ', '
        Write-Host "(WARN) Port listens only on: $addrs" -ForegroundColor Yellow
    } else {
        Write-Host '(FAIL) Port is not listening. Run start_server.bat first.' -ForegroundColor Red
    }
}

$ruleNames = @('GameServer TCP 8765 LAN', 'GameServer UDP 8765 LAN')
foreach ($name in $ruleNames) {
    $rule = Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($rule -and $rule.Enabled -eq 'True') {
        Write-Host "(OK) Firewall rule: $name" -ForegroundColor Green
    } else {
        Write-Host "(FAIL) Missing or disabled firewall rule: $name" -ForegroundColor Red
        Write-Host '       Run open_firewall_lan.bat or start_server.bat as Administrator.' -ForegroundColor Yellow
    }
}

$ips = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
    ForEach-Object { $_.IPAddress } | Sort-Object -Unique)

if ($ips.Count -eq 0) {
    Write-Host '(WARN) No LAN IPv4 detected' -ForegroundColor Yellow
} else {
    foreach ($ip in $ips) {
        $url = "http://${ip}:$port/health"
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -eq 200) {
                Write-Host "(OK) Health via LAN IP: $url" -ForegroundColor Green
            } else {
                Write-Host "(WARN) $url returned $($r.StatusCode)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "(FAIL) Cannot reach $url - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ''
Write-Host 'Teammate should open the download portal in browser:' -ForegroundColor Cyan
foreach ($ip in $ips) {
    Write-Host "  http://${ip}:$port/"
}
Write-Host ''
Write-Host 'Admin unified control panel (enter console command at portal footer):' -ForegroundColor Cyan
foreach ($ip in $ips) {
    Write-Host "  http://${ip}:$port/control/"
}
Write-Host ''
Write-Host 'Health check URL:' -ForegroundColor Cyan
foreach ($ip in $ips) {
    Write-Host "  http://${ip}:$port/health"
}
Write-Host ''
Write-Host "Unity baseUrl should match one of the LAN URLs above (project default: http://${expectedIp}:$port)" -ForegroundColor Cyan
if ($ips.Count -gt 0 -and ($ips -notcontains $expectedIp)) {
    Write-Host "(WARN) This PC is not $expectedIp - clients still pointing at $expectedIp will fail." -ForegroundColor Red

}