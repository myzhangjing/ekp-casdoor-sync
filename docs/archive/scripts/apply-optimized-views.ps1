# 通过环境变量 EKP_SQLSERVER_CONN 执行优化视图脚本
param(
    [string]$SqlFile = "$PSScriptRoot\OPTIMIZE_VIEWS_V2_PERFORMANCE.sql"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SqlFile)) {
  Write-Host "❌ 找不到优化脚本: $SqlFile" -ForegroundColor Red
  exit 1
}

$connStr = $env:EKP_SQLSERVER_CONN
if ([string]::IsNullOrWhiteSpace($connStr)) {
  Write-Host "❌ 缺少环境变量 EKP_SQLSERVER_CONN" -ForegroundColor Red
  Write-Host "请先设置 SQL Server 连接串后重试。" -ForegroundColor Yellow
  exit 1
}

Write-Host "读取 SQL 脚本..." -ForegroundColor Cyan
$sqlScript = Get-Content $SqlFile -Raw -Encoding UTF8

# 按 GO 分割批次（兼容末尾 GO）
$batches = $sqlScript -split '\r?\nGO\r?\n|\r?\nGO$'
Write-Host "共 $($batches.Count) 个批次" -ForegroundColor Yellow

Add-Type -AssemblyName System.Data
$cn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$cn.Open()
Write-Host "✓ 已连接 SQL Server" -ForegroundColor Green

try {
  $i = 0
  foreach($batch in $batches){
    $sql = $batch.Trim()
    if ([string]::IsNullOrWhiteSpace($sql)) { continue }
    if ($sql.StartsWith('--')) { continue }

    $i++
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 300
    try {
      [void]$cmd.ExecuteNonQuery()
      Write-Host "  ✓ 批次 $i 执行成功" -ForegroundColor Green
    } finally {
      $cmd.Dispose()
    }
  }
  Write-Host "\n✅ 优化视图全部应用完成" -ForegroundColor Green
}
finally {
  $cn.Close(); $cn.Dispose()
}
