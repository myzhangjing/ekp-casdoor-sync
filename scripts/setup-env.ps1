# EKP-Casdoor 同步工具 - 环境变量配置脚本
# 使用方法：修改下面的值，然后在 PowerShell 中运行此脚本

# =============================================
# Casdoor 配置（必需）
# =============================================

# Casdoor 服务器地址
$env:CASDOOR_ENDPOINT = "http://sso.fzcsps.com"

# Casdoor 应用 Client ID
$env:CASDOOR_CLIENT_ID = "aecd00a352e5c560ffe6"

# Casdoor 应用 Client Secret
$env:CASDOOR_CLIENT_SECRET = "your-client-secret-here"

# Casdoor 组织 Owner
$env:CASDOOR_DEFAULT_OWNER = "fzswjtOrganization"

# =============================================
# EKP 数据库配置（可选，也可在界面中配置）
# =============================================

# EKP SQL Server 连接字符串
# $env:EKP_SQLSERVER_CONN = "Server=192.168.1.100,1433;Database=ekp;User Id=sa;Password=****;TrustServerCertificate=True;"

# 用户-组织关系视图名称
# $env:EKP_USER_GROUP_VIEW = "vw_user_group_membership"

# =============================================
# 说明
# =============================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  环境变量配置脚本" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "✅ Casdoor 配置已设置（当前 PowerShell 会话）" -ForegroundColor Green
Write-Host ""
Write-Host "  CASDOOR_ENDPOINT = $env:CASDOOR_ENDPOINT" -ForegroundColor Gray
Write-Host "  CASDOOR_CLIENT_ID = $env:CASDOOR_CLIENT_ID" -ForegroundColor Gray
Write-Host "  CASDOOR_CLIENT_SECRET = $(if($env:CASDOOR_CLIENT_SECRET.Length -gt 0){'[已设置]'}else{'[未设置]'})" -ForegroundColor Gray
Write-Host "  CASDOOR_DEFAULT_OWNER = $env:CASDOOR_DEFAULT_OWNER" -ForegroundColor Gray
Write-Host ""

Write-Host "⚠️  注意：" -ForegroundColor Yellow
Write-Host "  1. 这些环境变量仅在当前 PowerShell 会话中有效" -ForegroundColor Yellow
Write-Host "  2. 关闭 PowerShell 后需要重新设置" -ForegroundColor Yellow
Write-Host ""

Write-Host "💡 如需永久设置（推荐）：" -ForegroundColor Cyan
Write-Host "  使用以下命令设置系统级环境变量：" -ForegroundColor White
Write-Host ""
Write-Host '  [System.Environment]::SetEnvironmentVariable("CASDOOR_ENDPOINT", "http://sso.fzcsps.com", "Machine")' -ForegroundColor DarkGray
Write-Host '  [System.Environment]::SetEnvironmentVariable("CASDOOR_CLIENT_ID", "aecd00a352e5c560ffe6", "Machine")' -ForegroundColor DarkGray
Write-Host '  [System.Environment]::SetEnvironmentVariable("CASDOOR_CLIENT_SECRET", "your-secret", "Machine")' -ForegroundColor DarkGray
Write-Host '  [System.Environment]::SetEnvironmentVariable("CASDOOR_DEFAULT_OWNER", "fzswjtOrganization", "Machine")' -ForegroundColor DarkGray
Write-Host ""
Write-Host "  ⚠️  设置系统级环境变量需要管理员权限" -ForegroundColor Yellow
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  现在可以启动应用程序了！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 提示是否立即启动应用
$response = Read-Host "是否立即启动应用？(Y/N)"
if ($response -eq 'Y' -or $response -eq 'y') {
    $exePath = Join-Path $PSScriptRoot "..\SyncEkpToCasdoor\SyncEkpToCasdoor.UI\bin\Release\net8.0-windows\SyncEkpToCasdoor.UI.exe"
    if (Test-Path $exePath) {
        Write-Host "正在启动应用..." -ForegroundColor Green
        Start-Process $exePath
    } else {
        Write-Host "未找到应用程序，请先编译项目：" -ForegroundColor Red
        Write-Host "  dotnet build SyncEkpToCasdoor.sln -c Release" -ForegroundColor White
    }
}
