$ErrorActionPreference = "Stop"

# 全量同步：将 SYNC_SINCE_UTC 设为 Unix 纪元时间，强制同步所有数据
$env:SYNC_SINCE_UTC = "1970-01-01T00:00:00Z"
Write-Host "执行全量同步（SYNC_SINCE_UTC=$env:SYNC_SINCE_UTC）" -ForegroundColor Yellow

# 调用主同步脚本
$scriptsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$mainScript = Join-Path $scriptsDir "run-sync.ps1"

if (-not (Test-Path $mainScript)) {
  Write-Error "未找到主同步脚本: $mainScript"
  exit 1
}

& $mainScript
