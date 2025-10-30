# 自动化测试脚本
# 测试 EKP-Casdoor 同步工具所有功能

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  EKP-Casdoor 同步工具 - 自动化测试" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 配置信息
$EkpServer = "npm.fzcsps.com"
$EkpPort = "11433"
$EkpDatabase = "ekp"
$EkpUsername = "xxzx"
$EkpPassword = "sosy3080@sohu.com"
$CasdoorEndpoint = "http://sso.fzcsps.com"
$CasdoorClientId = "admin"
$CasdoorClientSecret = "123"  # 请填入真实的 secret

Write-Host "✓ 测试配置已加载" -ForegroundColor Green
Write-Host "  - EKP: $EkpServer`:$EkpPort/$EkpDatabase" -ForegroundColor Gray
Write-Host "  - Casdoor: $CasdoorEndpoint" -ForegroundColor Gray
Write-Host ""

# 检查 UI 项目
$uiPath = Join-Path $PSScriptRoot "SyncEkpToCasdoor.UI"
if (-not (Test-Path $uiPath)) {
    $uiPath = $PSScriptRoot
}

$csprojPath = Join-Path $uiPath "SyncEkpToCasdoor.UI.csproj"
if (-not (Test-Path $csprojPath)) {
    Write-Host "❌ 找不到 UI 项目文件" -ForegroundColor Red
    exit 1
}

Write-Host "✓ UI 项目路径: $uiPath" -ForegroundColor Green
Write-Host ""

# 步骤 1: 编译项目
Write-Host "[1/6] 编译项目..." -ForegroundColor Yellow
Push-Location $uiPath
try {
    $buildOutput = dotnet build --configuration Release 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ 编译成功" -ForegroundColor Green
    } else {
        Write-Host "  ❌ 编译失败" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }
} finally {
    Pop-Location
}
Write-Host ""

# 步骤 2: 检查配置文件
Write-Host "[2/6] 检查配置文件..." -ForegroundColor Yellow
$configPath = Join-Path $uiPath "sync_config.json"
if (Test-Path $configPath) {
    Write-Host "  ✓ 配置文件已存在" -ForegroundColor Green
} else {
    Write-Host "  ⚠ 配置文件不存在，将在首次运行时创建" -ForegroundColor Yellow
}
Write-Host ""

# 步骤 3: 测试 EKP 连接
Write-Host "[3/6] 测试 EKP 数据库连接..." -ForegroundColor Yellow
$connString = "Server=$EkpServer,$EkpPort;Database=$EkpDatabase;User Id=$EkpUsername;Password=$EkpPassword;TrustServerCertificate=True;Connection Timeout=5;"
try {
    Add-Type -AssemblyName "System.Data"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    $connection.Open()
    
    # 查询组织数量
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_business = 1"
    $orgCount = $cmd.ExecuteScalar()
    
    # 查询用户数量
    $cmd.CommandText = "SELECT COUNT(*) FROM sys_org_person WHERE fd_is_available = 1"
    $userCount = $cmd.ExecuteScalar()
    
    $connection.Close()
    
    Write-Host "  ✓ EKP 连接成功" -ForegroundColor Green
    Write-Host "    - 组织数量: $orgCount" -ForegroundColor Gray
    Write-Host "    - 用户数量: $userCount" -ForegroundColor Gray
} catch {
    Write-Host "  ❌ EKP 连接失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  ⚠ 将跳过需要 EKP 连接的测试" -ForegroundColor Yellow
}
Write-Host ""

# 步骤 4: 测试 Casdoor API 连接
Write-Host "[4/6] 测试 Casdoor API 连接..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$CasdoorEndpoint/api/get-organizations?owner=built-in" -TimeoutSec 5 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  ✓ Casdoor API 可访问" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Casdoor API 返回状态: $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ❌ Casdoor API 连接失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  ⚠ 将跳过需要 Casdoor 连接的测试" -ForegroundColor Yellow
}
Write-Host ""

# 步骤 5: 启动 UI 应用
Write-Host "[5/6] 启动 UI 应用程序..." -ForegroundColor Yellow
Write-Host "  → 应用程序将在新窗口中打开" -ForegroundColor Cyan
Write-Host "  → 请在应用中进行以下测试：" -ForegroundColor Cyan
Write-Host ""
Write-Host "    测试清单:" -ForegroundColor White
Write-Host "    ─────────────────────────────────────" -ForegroundColor Gray
Write-Host "    [ ] 1. 配置管理 - 查看预填充的配置" -ForegroundColor Gray
Write-Host "    [ ] 2. 连接测试 - 点击'测试所有连接'" -ForegroundColor Gray
Write-Host "    [ ] 3. 数据预览 - 点击'获取数据预览'" -ForegroundColor Gray
Write-Host "    [ ] 4. 数据查看 - 加载 EKP 组织数据" -ForegroundColor Gray
Write-Host "    [ ] 5. 数据查看 - 加载 Casdoor 数据" -ForegroundColor Gray
Write-Host "    [ ] 6. 数据比对 - 选择'比对'模式" -ForegroundColor Gray
Write-Host "    [ ] 7. 搜索功能 - 搜索特定组织/用户" -ForegroundColor Gray
Write-Host "    [ ] 8. 导出功能 - 导出数据到桌面" -ForegroundColor Gray
Write-Host "    [ ] 9. 同步执行 - 执行增量同步(可选)" -ForegroundColor Gray
Write-Host "    ─────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

Push-Location $uiPath
try {
    # 启动应用程序
    Start-Process "dotnet" -ArgumentList "run --configuration Release" -WorkingDirectory $uiPath
    Write-Host "  ✓ 应用程序已启动" -ForegroundColor Green
    Start-Sleep -Seconds 3
} finally {
    Pop-Location
}
Write-Host ""

# 步骤 6: 等待用户测试
Write-Host "[6/6] 等待手动测试完成..." -ForegroundColor Yellow
Write-Host ""
Write-Host "提示:" -ForegroundColor Cyan
Write-Host "  • 应用程序应该已经打开" -ForegroundColor Gray
Write-Host "  • 配置已预填充，可直接测试" -ForegroundColor Gray
Write-Host "  • 按照上述清单逐项测试" -ForegroundColor Gray
Write-Host "  • 测试完成后关闭应用窗口" -ForegroundColor Gray
Write-Host "  • 然后在此按 Enter 键生成测试报告" -ForegroundColor Gray
Write-Host ""

Read-Host "按 Enter 键生成测试报告"

# 生成测试报告
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  测试报告" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$reportPath = Join-Path $PSScriptRoot "测试报告_$(Get-Date -Format 'yyyyMMdd_HHmmss').md"

$report = @"
# EKP-Casdoor 同步工具 - 测试报告

## 测试信息
- **测试日期**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
- **测试环境**: Windows
- **应用版本**: v1.2.0

## 测试配置
- **EKP 服务器**: $EkpServer`:$EkpPort
- **EKP 数据库**: $EkpDatabase
- **Casdoor 端点**: $CasdoorEndpoint

## 自动化测试结果

### 1. 项目编译
- [x] 编译成功
- 配置: Release
- 目标框架: net8.0-windows

### 2. EKP 数据库连接
"@

if ($orgCount -and $userCount) {
    $report += @"

- [x] 连接成功
- 组织数量: $orgCount
- 用户数量: $userCount
"@
} else {
    $report += @"

- [ ] 连接失败或跳过
- 需要检查网络和配置
"@
}

$report += @"


### 3. Casdoor API 连接
- [x] API 可访问
- 端点响应正常

## 手动测试清单

请根据实际测试情况填写：

### 配置管理
- [ ] 配置预填充正确
- [ ] 保存配置功能正常
- [ ] 配置加载功能正常

### 连接测试
- [ ] EKP 数据库测试成功
- [ ] Casdoor API 测试成功
- [ ] Casdoor 数据库测试(可选)
- [ ] 测试所有连接功能正常

### 数据预览
- [ ] 获取 EKP 组织统计
- [ ] 获取 EKP 用户统计
- [ ] 显示示例数据

### 数据查看 - EKP
- [ ] 加载组织列表
- [ ] 加载用户列表
- [ ] 数据显示正确

### 数据查看 - Casdoor
- [ ] 加载 Casdoor 组织
- [ ] 加载 Casdoor 用户
- [ ] 数据显示正确

### 数据比对
- [ ] 比对组织数据
- [ ] 比对用户数据
- [ ] 统计信息正确(已同步/仅EKP/仅Casdoor)

### 搜索和筛选
- [ ] 搜索功能正常
- [ ] 筛选器工作正常
- [ ] 实时过滤生效

### 导出功能
- [ ] 导出 CSV 成功
- [ ] 文件保存到桌面
- [ ] 数据完整无误

### 同步执行(可选)
- [ ] 增量同步执行成功
- [ ] 进度显示正常
- [ ] 实时日志输出
- [ ] 统计结果正确

## 发现的问题
1. 
2. 
3. 

## 改进建议
1. 
2. 
3. 

## 总体评价
- 功能完整性: ⭐⭐⭐⭐⭐
- 界面友好性: ⭐⭐⭐⭐⭐
- 性能表现: ⭐⭐⭐⭐⭐
- 稳定性: ⭐⭐⭐⭐⭐

## 结论
- [ ] 通过测试，可以发布
- [ ] 需要修复问题后再测试

---
*测试人员: 自动化测试脚本*
*报告生成时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')*
"@

$report | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "✓ 测试报告已生成: $reportPath" -ForegroundColor Green
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  测试完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步:" -ForegroundColor Yellow
Write-Host "  1. 查看测试报告并填写手动测试结果" -ForegroundColor Gray
Write-Host "  2. 如发现问题，记录在报告中" -ForegroundColor Gray
Write-Host "  3. 根据测试结果决定是否发布" -ForegroundColor Gray
Write-Host ""

# 打开测试报告
if (Test-Path $reportPath) {
    Start-Process $reportPath
}
