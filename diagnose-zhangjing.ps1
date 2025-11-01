# 诊断脚本：查询"张璟"未同步的原因
# 日期: 2025-10-31

Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "EKP用户同步诊断工具" -ForegroundColor Green
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# 设置环境变量
$env:EKP_SQLSERVER_CONN = "Server=192.168.1.100,1433;Database=ekp;User Id=sa;Password=Landray@1234;TrustServerCertificate=True;"

$exePath = "c:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\bin\Release\net8.0\SyncEkpToCasdoor.exe"

# 检查可执行文件是否存在
if (-not (Test-Path $exePath)) {
    Write-Host "错误: 找不到可执行文件" -ForegroundColor Red
    Write-Host "路径: $exePath" -ForegroundColor Yellow
    exit 1
}

Write-Host "步骤 1: 查询用户视图 (vw_casdoor_users_sync)" -ForegroundColor Yellow
Write-Host "-" * 80
Write-Host ""

$output1 = & $exePath --peek-user 张璟 2>&1
$output1 | ForEach-Object { Write-Host $_ }

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# 检查是否找到用户
if ($output1 -match "找到.*条记录" -or $output1 -match "username") {
    Write-Host "✓ 在用户视图中找到了记录" -ForegroundColor Green
    Write-Host ""
    
    # 尝试从输出中提取username
    $username = $null
    foreach ($line in $output1) {
        if ($line -match "^\s*(\w+)\s+\|") {
            $username = $Matches[1]
            break
        }
    }
    
    if ($username) {
        Write-Host "步骤 2: 查询成员关系视图 (vw_user_group_membership)" -ForegroundColor Yellow
        Write-Host "用户名: $username" -ForegroundColor Cyan
        Write-Host "-" * 80
        Write-Host ""
        
        $output2 = & $exePath --peek-membership $username 2>&1
        $output2 | ForEach-Object { Write-Host $_ }
        
        Write-Host ""
        Write-Host "=" * 80 -ForegroundColor Cyan
    } else {
        Write-Host "⚠ 无法提取username，跳过成员关系查询" -ForegroundColor Yellow
    }
} else {
    Write-Host "✗ 用户未在视图中找到" -ForegroundColor Red
    Write-Host ""
    Write-Host "可能原因:" -ForegroundColor Yellow
    Write-Host "  1. 用户未设置登录名 (fd_login_name)" -ForegroundColor Gray
    Write-Host "  2. 用户未关联有效部门" -ForegroundColor Gray
    Write-Host "  3. 部门不在目标公司层级下" -ForegroundColor Gray
    Write-Host "  4. 视图 vw_casdoor_users_sync 未正确配置" -ForegroundColor Gray
    Write-Host ""
}

Write-Host ""
Write-Host "诊断完成！" -ForegroundColor Green
Write-Host "=" * 80 -ForegroundColor Cyan
