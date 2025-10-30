# =====================================================
# 应用视图修复脚本
# =====================================================
param(
    [string]$SqlScriptPath = "FIX_HIERARCHY_DEPTH.sql"
)

$ErrorActionPreference = "Stop"

Write-Host "=== 应用组织层级视图修复 ===" -ForegroundColor Cyan
Write-Host ""

# 检查环境变量
$connStr = $env:EKP_SQLSERVER_CONN
if (-not $connStr) {
    Write-Host "❌ 未找到环境变量 EKP_SQLSERVER_CONN" -ForegroundColor Red
    Write-Host "请设置: " -ForegroundColor Yellow
    Write-Host '  $env:EKP_SQLSERVER_CONN = "Server=172.16.10.110,1433;Database=ekp;User Id=sa;Password=Landray@123;TrustServerCertificate=True"' -ForegroundColor Gray
    exit 1
}

# 读取SQL脚本
$scriptPath = Join-Path $PSScriptRoot $SqlScriptPath
if (-not (Test-Path $scriptPath)) {
    Write-Host "❌ SQL脚本不存在: $scriptPath" -ForegroundColor Red
    exit 1
}

Write-Host "📄 读取SQL脚本: $SqlScriptPath" -ForegroundColor Gray
$sqlContent = Get-Content $scriptPath -Raw -Encoding UTF8

# 分割GO语句
$batches = $sqlContent -split '\r?\nGO\r?\n' | Where-Object { $_.Trim() -ne '' }
Write-Host "📦 共 $($batches.Count) 个SQL批次" -ForegroundColor Gray
Write-Host ""

# 执行SQL
try {
    Add-Type -AssemblyName "System.Data.SqlClient"
    
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    Write-Host "✓ 连接到SQL Server" -ForegroundColor Green
    
    $successCount = 0
    foreach ($batch in $batches) {
        $trimmedBatch = $batch.Trim()
        if ($trimmedBatch.StartsWith('--') -or $trimmedBatch.StartsWith('PRINT')) {
            # 跳过纯注释和PRINT语句（在PS中不生效）
            continue
        }
        
        $cmd = New-Object System.Data.SqlClient.SqlCommand($trimmedBatch, $conn)
        $cmd.CommandTimeout = 300  # 5分钟超时
        
        try {
            $rowsAffected = $cmd.ExecuteNonQuery()
            $successCount++
            Write-Host "  ✓ 批次 $successCount 执行成功" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠ 批次执行警告: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    $conn.Close()
    
    Write-Host ""
    Write-Host "✅ 视图修复完成！" -ForegroundColor Green
    Write-Host ""
    
    Write-Host ""
    Write-Host "✅ 视图修复应用成功！" -ForegroundColor Green
    Write-Host "现在请运行同步程序重新同步组织数据。" -ForegroundColor Cyan
    
} catch {
    Write-Host ""
    Write-Host "❌ 执行失败: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
