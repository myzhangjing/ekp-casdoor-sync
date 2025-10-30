# 执行视图优化脚本
param(
    [string]$SqlFile = "c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\OPTIMIZE_VIEWS_V2_PERFORMANCE.sql"
)

$connectionString = "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=sosy3080@sohu.com;TrustServerCertificate=True;"

Write-Host "Reading SQL script..." -ForegroundColor Cyan
$sqlScript = Get-Content $SqlFile -Raw -Encoding UTF8

# 按GO分割批次
$batches = $sqlScript -split '\r?\nGO\r?\n|\r?\nGO$'

Write-Host "Total $($batches.Count) SQL batches" -ForegroundColor Yellow
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    
    # 注册消息事件
    $connection.add_InfoMessage({
        param($sender, $e)
        Write-Host $e.Message -ForegroundColor Gray
    })
    
    $connection.Open()
    Write-Host "Database connected successfully" -ForegroundColor Green
    Write-Host ""
    
    $batchNum = 0
    foreach ($batch in $batches) {
        $trimmedBatch = $batch.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedBatch) -or $trimmedBatch.StartsWith("--")) {
            continue
        }
        
        $batchNum++
        try {
            $command = $connection.CreateCommand()
            $command.CommandText = $trimmedBatch
            $command.CommandTimeout = 300  # 5分钟超时
            
            $result = $command.ExecuteNonQuery()
            
        } catch {
            Write-Host "Batch $batchNum failed: $($_.Exception.Message)" -ForegroundColor Red
            throw
        } finally {
            if ($command) { $command.Dispose() }
        }
    }
    
    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "View optimization completed!" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if ($connection) {
        $connection.Close()
        $connection.Dispose()
    }
}
