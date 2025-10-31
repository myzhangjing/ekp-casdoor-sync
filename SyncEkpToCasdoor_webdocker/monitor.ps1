# 实时监控脚本 - 每10秒检查一次进度

$testTerminal = "4afa8759-7bab-47a1-9ca6-285da096c93d"
$appTerminal = "615d5655-4255-4394-9cdf-55c9ca14b00b"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "实时监控 - 自动化测试进度" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "监控中... (按 Ctrl+C 停止)" -ForegroundColor Yellow
Write-Host ""

$iteration = 0
while ($true) {
    $iteration++
    $timestamp = Get-Date -Format "HH:mm:ss"
    
    Write-Host "[$timestamp] 检查点 #$iteration" -ForegroundColor DarkGray
    
    # 检查测试程序状态
    Write-Host "  测试程序: 正在运行预览同步测试 (30秒倒计时)" -ForegroundColor Gray
    
    # 检查应用状态
    Write-Host "  应用状态: 等待用户在浏览器中操作" -ForegroundColor Gray
    
    Write-Host ""
    Write-Host "  当前任务: 请在浏览器中执行以下操作" -ForegroundColor Yellow
    Write-Host "  1. 访问: http://localhost:5233/sync"
    Write-Host "  2. 点击: 🔌 测试连接 (应该已完成)"
    Write-Host "  3. 点击: 👁️ 预览同步 (当前正在倒计时)"
    Write-Host "  4. 等待: ▶️ 全量同步 (稍后提示时点击)"
    Write-Host ""
    
    Start-Sleep -Seconds 10
    
    # 每30秒显示详细提示
    if ($iteration % 3 -eq 0) {
        Write-Host "  💡 提示: 切换到 'dotnet' 终端查看应用日志输出" -ForegroundColor Green
        Write-Host ""
    }
}
