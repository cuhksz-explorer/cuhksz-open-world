$ErrorActionPreference = 'Stop'
$port = 8765
if ($env:GAME_SERVER_PORT) { $port = [int]$env:GAME_SERVER_PORT }
$ok = $false
for ($i = 0; $i -lt 25; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://127.0.0.1:$port/health" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ok = $true; break }
    } catch {}
    Start-Sleep -Milliseconds 300
}
if (-not $ok) { exit 1 }
