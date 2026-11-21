param([int]$Port = 8765)
$ErrorActionPreference = 'SilentlyContinue'
$conns = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
if ($conns.Count -eq 0) {
    Write-Host "[GameServer] No process listening on port $Port"
    exit 0
}
$pids = @($conns | ForEach-Object { $_.OwningProcess } | Sort-Object -Unique)
foreach ($pid in $pids) {
    Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
    Write-Host "[GameServer] Stopped PID $pid"
}
Start-Sleep -Milliseconds 500
$still = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
if ($still.Count -gt 0) {
    Write-Host "[GameServer] WARN - Port $Port still in use"
    exit 1
}
Write-Host "[GameServer] Port $Port released"
exit 0
