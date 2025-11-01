# 测试修复效果
$ErrorActionPreference = "Continue"
$output = @()

$output += "========== 修复验证测试 =========="
$output += "测试时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$output += ""

# 1. 检查程序文件
$exePath = "c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\bin\Release\net8.0\SyncEkpToCasdoor.exe"
if (Test-Path $exePath) {
    $fileInfo = Get-Item $exePath
    $output += "✓ 程序文件存在"
    $output += "  路径: $exePath"
    $output += "  最后修改: $($fileInfo.LastWriteTime)"
} else {
    $output += "✗ 程序文件不存在: $exePath"
}
$output += ""

# 2. 运行应用视图命令
$output += "========== 应用修复后的视图 =========="
try {
    cd "c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\bin\Release\net8.0"
    $result = & .\SyncEkpToCasdoor.exe --apply-optimized-views 2>&1 | Out-String
    $output += $result
} catch {
    $output += "错误: $_"
}
$output += ""

# 3. 查询张璟
$output += "========== 查询张璟 =========="
try {
    $result = & .\SyncEkpToCasdoor.exe --peek-user 张璟 2>&1 | Out-String
    $output += $result
} catch {
    $output += "错误: $_"
}
$output += ""

# 4. 查询技术管理部
$output += "========== 查询技术管理部 =========="
try {
    $result = & .\SyncEkpToCasdoor.exe --peek-user 技术管理 2>&1 | Out-String
    $output += $result
} catch {
    $output += "错误: $_"
}

# 保存结果
$reportPath = "c:\Users\ThinkPad\Desktop\test-report.txt"
$output | Out-File -FilePath $reportPath -Encoding UTF8
Write-Host "测试完成，报告已保存到: $reportPath"
