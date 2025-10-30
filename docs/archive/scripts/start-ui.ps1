# EKP-Casdoor 同步工具 - 配置界面启动脚本

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  EKP-Casdoor 同步工具 - 配置界面  " -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 检查 .NET 8 SDK
Write-Host "检查 .NET 8 SDK..." -NoNewline
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -eq 0 -and $dotnetVersion -match "^8\.") {
    Write-Host " ✓" -ForegroundColor Green
    Write-Host "  版本: $dotnetVersion" -ForegroundColor Gray
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host ""
    Write-Host "错误: 未找到 .NET 8 SDK" -ForegroundColor Red
    Write-Host "请从以下地址下载安装: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "按 Enter 键退出"
    exit 1
}

# 检查项目文件
$projectPath = Join-Path $PSScriptRoot "SyncEkpToCasdoor.UI\SyncEkpToCasdoor.UI.csproj"
Write-Host "检查项目文件..." -NoNewline
if (Test-Path $projectPath) {
    Write-Host " ✓" -ForegroundColor Green
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host ""
    Write-Host "错误: 未找到项目文件" -ForegroundColor Red
    Write-Host "路径: $projectPath" -ForegroundColor Gray
    Write-Host ""
    Read-Host "按 Enter 键退出"
    exit 1
}

# 编译项目
Write-Host ""
Write-Host "正在编译项目..." -ForegroundColor Yellow

$buildOutput = dotnet build $projectPath -c Release 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "编译成功 ✓" -ForegroundColor Green
} else {
    Write-Host "编译失败 ✗" -ForegroundColor Red
    Write-Host ""
    Write-Host "编译输出:" -ForegroundColor Yellow
    Write-Host $buildOutput
    Write-Host ""
    Read-Host "按 Enter 键退出"
    exit 1
}

# 启动程序
Write-Host ""
Write-Host "正在启动配置界面..." -ForegroundColor Green
Write-Host ""

$exePath = Join-Path $PSScriptRoot "SyncEkpToCasdoor.UI\bin\Release\net8.0-windows\SyncEkpToCasdoor.UI.exe"

if (Test-Path $exePath) {
    # 使用 Start-Process 在新进程中启动，避免阻塞脚本
    Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath)
    
    Write-Host "配置界面已启动！" -ForegroundColor Green
    Write-Host ""
    Write-Host "提示:" -ForegroundColor Cyan
    Write-Host "  • 首次使用请先配置 EKP 和 Casdoor 连接信息" -ForegroundColor Gray
    Write-Host "  • 配置将加密保存到 sync_config.json" -ForegroundColor Gray
    Write-Host "  • 点击'测试连接'按钮验证配置" -ForegroundColor Gray
    Write-Host "  • 保存配置后可使用命令行工具执行同步" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "错误: 找不到可执行文件" -ForegroundColor Red
    Write-Host "路径: $exePath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "请尝试手动编译:" -ForegroundColor Yellow
    Write-Host "  cd SyncEkpToCasdoor.UI" -ForegroundColor Gray
    Write-Host "  dotnet build -c Release" -ForegroundColor Gray
    Write-Host ""
    Read-Host "按 Enter 键退出"
    exit 1
}
