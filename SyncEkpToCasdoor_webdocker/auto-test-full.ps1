# =====================================
# 完全自动化测试脚本 (完整版)
# =====================================

$baseUrl = "http://localhost:5233"
$apiUrl = "$baseUrl/api/sync"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  EKP-Casdoor 同步系统自动化测试" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 测试计数器
$testsPassed = 0
$testsFailed = 0
$testsTotal = 7

function Write-TestHeader {
    param([string]$testName, [int]$testNumber)
    Write-Host ""
    Write-Host "[$testNumber/$testsTotal] $testName" -ForegroundColor Yellow
    Write-Host "-------------------------------------" -ForegroundColor Gray
}

function Write-Success {
    param([string]$message)
    Write-Host "  ✓ $message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$message)
    Write-Host "  ✗ $message" -ForegroundColor Red
}

function Write-Info {
    param([string]$message)
    Write-Host "  ℹ $message" -ForegroundColor Cyan
}

function Wait-WithProgress {
    param([int]$seconds, [string]$message)
    Write-Info $message
    for ($i = $seconds; $i -gt 0; $i--) {
        Write-Host "`r  等待: $i 秒..." -NoNewline -ForegroundColor Gray
        Start-Sleep -Seconds 1
    }
    Write-Host "`r  等待: 完成    " -ForegroundColor Green
}

# =====================================
# 测试 1: 检查应用是否运行
# =====================================
Write-TestHeader "检查应用运行状态" 1
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/sync" -TimeoutSec 5 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Success "应用运行正常 (http://localhost:5233)"
        $testsPassed++
    }
} catch {
    Write-Failure "应用未运行或无法访问"
    Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor DarkRed
    $testsFailed++
    Write-Host ""
    Write-Host "请先启动应用:" -ForegroundColor Yellow
    Write-Host "  cd SyncEkpToCasdoor.Web" -ForegroundColor White
    Write-Host "  dotnet run" -ForegroundColor White
    exit 1
}

# =====================================
# 测试 2: 测试连接 (API)
# =====================================
Write-TestHeader "测试 EKP 和 Casdoor 连接" 2
try {
    Write-Info "调用 API: GET $apiUrl/test-connections"
    $response = Invoke-RestMethod -Uri "$apiUrl/test-connections" -Method Get -TimeoutSec 30
    
    if ($response.success) {
        Write-Success "连接测试通过"
        Write-Info "EKP 数据库: ✓"
        Write-Info "Casdoor API: ✓"
        if ($response.details) {
            foreach ($detail in $response.details) {
                Write-Host "    - $detail" -ForegroundColor Gray
            }
        }
        $testsPassed++
    } else {
        Write-Failure "连接测试失败: $($response.message)"
        $testsFailed++
    }
} catch {
    Write-Failure "API 调用失败: $($_.Exception.Message)"
    Write-Info "可能 API 端点未创建，请先运行: .\auto-test.ps1"
    $testsFailed++
}

# =====================================
# 测试 3: 预览同步 (API)
# =====================================
Write-TestHeader "预览同步数据" 3
try {
    Write-Info "调用 API: GET $apiUrl/preview"
    $response = Invoke-RestMethod -Uri "$apiUrl/preview" -Method Get -TimeoutSec 60
    
    if ($response.success) {
        Write-Success "预览同步完成"
        if ($response.details) {
            foreach ($detail in $response.details) {
                Write-Host "    - $detail" -ForegroundColor Gray
            }
        }
        $testsPassed++
    } else {
        Write-Failure "预览同步失败: $($response.message)"
        $testsFailed++
    }
} catch {
    Write-Failure "API 调用失败: $($_.Exception.Message)"
    $testsFailed++
}

# =====================================
# 测试 4: 获取初始状态
# =====================================
Write-TestHeader "检查同步状态" 4
try {
    Write-Info "调用 API: GET $apiUrl/status"
    $statusBefore = Invoke-RestMethod -Uri "$apiUrl/status" -Method Get -TimeoutSec 10
    
    Write-Info "当前状态: $(if($statusBefore.IsRunning){'运行中'}else{'空闲'})"
    if ($statusBefore.LastSyncTime) {
        Write-Info "上次同步: $($statusBefore.LastSyncTime)"
    }
    if ($statusBefore.SyncStartTime) {
        Write-Info "同步开始: $($statusBefore.SyncStartTime)"
    }
    
    if (-not $statusBefore.IsRunning) {
        Write-Success "状态正常（空闲）"
        $testsPassed++
    } else {
        Write-Failure "同步正在运行中，无法开始新测试"
        $testsFailed++
    }
} catch {
    Write-Failure "获取状态失败: $($_.Exception.Message)"
    $testsFailed++
}

# =====================================
# 测试 5: 执行完整同步
# =====================================
Write-TestHeader "执行完整同步" 5
try {
    Write-Info "调用 API: POST $apiUrl/full-sync"
    Write-Info "这可能需要几分钟时间..."
    
    # 异步启动同步
    $job = Start-Job -ScriptBlock {
        param($url)
        Invoke-RestMethod -Uri $url -Method Post -TimeoutSec 600
    } -ArgumentList "$apiUrl/full-sync"
    
    # 监控进度
    $startTime = Get-Date
    $maxWaitSeconds = 300  # 最多等待5分钟
    $checkInterval = 5
    $lastLogCount = 0
    
    Write-Info "监控同步进度..."
    while ($job.State -eq 'Running') {
        $elapsed = ((Get-Date) - $startTime).TotalSeconds
        
        if ($elapsed -gt $maxWaitSeconds) {
            Write-Failure "同步超时（超过 $maxWaitSeconds 秒）"
            Stop-Job -Job $job
            Remove-Job -Job $job
            $testsFailed++
            break
        }
        
        # 获取实时日志
        try {
            $logs = Invoke-RestMethod -Uri "$apiUrl/logs?count=10" -Method Get -TimeoutSec 5
            if ($logs.Count -gt $lastLogCount) {
                foreach ($log in $logs[0..([Math]::Min(4, $logs.Count-1))]) {
                    $level = $log.Level
                    $color = switch ($level) {
                        "Error" { "Red" }
                        "Warning" { "Yellow" }
                        default { "Cyan" }
                    }
                    Write-Host "    [$($log.Timestamp.ToString('HH:mm:ss'))] $($log.Message)" -ForegroundColor $color
                }
                $lastLogCount = $logs.Count
            }
        } catch {
            # 忽略日志获取错误
        }
        
        Write-Host "`r  已运行: $([int]$elapsed) 秒..." -NoNewline -ForegroundColor Gray
        Start-Sleep -Seconds $checkInterval
    }
    Write-Host ""
    
    # 获取结果
    if ($job.State -eq 'Completed') {
        $response = Receive-Job -Job $job
        Remove-Job -Job $job
        
        if ($response.success) {
            Write-Success "完整同步完成"
            if ($response.details) {
                foreach ($detail in $response.details) {
                    Write-Host "    - $detail" -ForegroundColor Gray
                }
            }
            $testsPassed++
        } else {
            Write-Failure "完整同步失败: $($response.message)"
            $testsFailed++
        }
    }
} catch {
    Write-Failure "同步执行失败: $($_.Exception.Message)"
    $testsFailed++
}

# =====================================
# 测试 6: 验证状态恢复
# =====================================
Write-TestHeader "验证同步状态恢复" 6
Wait-WithProgress 3 "等待状态更新..."
try {
    $statusAfter = Invoke-RestMethod -Uri "$apiUrl/status" -Method Get -TimeoutSec 10
    
    Write-Info "当前状态: $(if($statusAfter.IsRunning){'运行中'}else{'空闲'})"
    if ($statusAfter.LastSyncTime) {
        Write-Info "上次同步: $($statusAfter.LastSyncTime)"
    }
    
    if (-not $statusAfter.IsRunning) {
        Write-Success "状态已恢复为空闲"
        $testsPassed++
    } else {
        Write-Failure "状态未恢复（仍在运行中）"
        $testsFailed++
    }
} catch {
    Write-Failure "获取状态失败: $($_.Exception.Message)"
    $testsFailed++
}

# =====================================
# 测试 7: 并发保护测试
# =====================================
Write-TestHeader "测试并发保护机制" 7
try {
    Write-Info "启动第一个同步..."
    $job1 = Start-Job -ScriptBlock {
        param($url)
        try {
            Invoke-RestMethod -Uri $url -Method Post -TimeoutSec 60
        } catch {
            @{ success = $false; message = $_.Exception.Message }
        }
    } -ArgumentList "$apiUrl/full-sync"
    
    Start-Sleep -Seconds 2
    
    Write-Info "尝试启动第二个同步（应该被拒绝）..."
    $job2 = Start-Job -ScriptBlock {
        param($url)
        try {
            Invoke-RestMethod -Uri $url -Method Post -TimeoutSec 10
        } catch {
            @{ success = $false; message = $_.Exception.Message }
        }
    } -ArgumentList "$apiUrl/full-sync"
    
    # 等待第二个任务完成
    Wait-Job -Job $job2 -Timeout 15 | Out-Null
    $result2 = Receive-Job -Job $job2
    Remove-Job -Job $job2
    
    # 停止第一个任务
    Stop-Job -Job $job1
    Remove-Job -Job $job1
    
    if ($result2.success -eq $false -and $result2.message -like "*正在运行*") {
        Write-Success "并发保护生效（第二个请求被正确拒绝）"
        Write-Info "拒绝原因: $($result2.message)"
        $testsPassed++
    } else {
        Write-Failure "并发保护未生效"
        $testsFailed++
    }
} catch {
    Write-Failure "并发测试失败: $($_.Exception.Message)"
    $testsFailed++
}

# =====================================
# 测试总结
# =====================================
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  测试完成" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  总计: $testsTotal 个测试" -ForegroundColor White
Write-Host "  通过: $testsPassed" -ForegroundColor Green
Write-Host "  失败: $testsFailed" -ForegroundColor Red
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "✓ 所有测试通过!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ 部分测试失败，请检查日志" -ForegroundColor Red
    exit 1
}
