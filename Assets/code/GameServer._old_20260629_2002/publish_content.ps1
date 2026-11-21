# 校验 GameServer 网页管理后台与客户端版本 API
param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))),
    [int]$Port = 8765
)

$ErrorActionPreference = "Stop"
$base = "http://127.0.0.1:$Port"

Write-Host "检查 GameServer 管理后台与客户端版本 API ($base) ..."

try {
    $health = Invoke-RestMethod -Uri "$base/health" -TimeoutSec 5
    Write-Host "  /health OK"
} catch {
    Write-Error "GameServer 未运行或无法访问 $base/health"
}

try {
    $releases = Invoke-RestMethod -Uri "$base/api/admin/client/releases" -TimeoutSec 5
    Write-Host "  当前激活版本: $($releases.activeVersion)"
    Write-Host "  已发布版本数: $($releases.releases.Count)"
} catch {
    Write-Warning "  /api/admin/client/releases 失败: $_"
}

try {
    $ver = Invoke-RestMethod -Uri "$base/api/client/version?current=0.0.0" -TimeoutSec 5
    Write-Host "  最新客户端版本: $($ver.latestVersion) (需更新: $($ver.updateRequired))"
} catch {
    Write-Warning "  /api/client/version 失败: $_"
}

Write-Host ""
Write-Host "管理后台: $base/admin/"
