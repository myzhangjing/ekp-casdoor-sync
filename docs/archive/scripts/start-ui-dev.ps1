# EKP-Casdoor 同步工具 - 开发模式启动脚本

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host " EKP-Casdoor 配置界面 (开发模式) " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$projectPath = Join-Path $PSScriptRoot "SyncEkpToCasdoor.UI\SyncEkpToCasdoor.UI.csproj"

Write-Host "项目路径: $projectPath" -ForegroundColor Gray
Write-Host ""
Write-Host "正在启动开发模式..." -ForegroundColor Yellow
Write-Host ""

# 使用 dotnet run 启动（开发模式，支持热重载）
dotnet run --project $projectPath

Write-Host ""
Write-Host "程序已退出。" -ForegroundColor Gray
