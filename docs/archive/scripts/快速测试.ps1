# 快速连接测试
# 使用明文密码直接测试 EKP 和 Casdoor 连接

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   EKP-Casdoor 快速连接测试" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# EKP 配置 (从前面的对话中获取)
$ekpServer = "npm.fzcsps.com"
$ekpPort = "11433"
$ekpDatabase = "ekp"
$ekpUsername = "xxzx"
$ekpPassword = "sosy3080@sohu.com"

# Casdoor 配置
$casdoorEndpoint = "http://sso.fzcsps.com"
$casdoorOwner = "built-in"

$passed = 0
$failed = 0

# 测试 EKP 数据库连接
Write-Host "[1] 测试 EKP 数据库连接..." -NoNewline
try {
    $connString = "Server=$ekpServer,$ekpPort;Database=$ekpDatabase;User Id=$ekpUsername;Password=$ekpPassword;TrustServerCertificate=True;Connection Timeout=15;"
    
    Add-Type -AssemblyName "System.Data"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    $connection.Open()
    
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT 1 AS TestValue"
    $result = $cmd.ExecuteScalar()
    
    $connection.Close()
    
    if ($result -eq 1) {
        Write-Host " ✓ 通过" -ForegroundColor Green
        $passed++
    } else {
        Write-Host " ✗ 失败 (查询结果异常)" -ForegroundColor Red
        $failed++
    }
} catch {
    Write-Host " ✗ 失败" -ForegroundColor Red
    Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor Red
    $failed++
}

# 查询 EKP 组织
Write-Host "[2] 查询 EKP 组织数量..." -NoNewline
try {
    $connString = "Server=$ekpServer,$ekpPort;Database=$ekpDatabase;User Id=$ekpUsername;Password=$ekpPassword;TrustServerCertificate=True;Connection Timeout=15;"
    
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    $connection.Open()
    
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_business = 1"
    $count = $cmd.ExecuteScalar()
    
    $connection.Close()
    
    Write-Host " ✓ 找到 $count 个组织" -ForegroundColor Green
    $passed++
} catch {
    Write-Host " ✗ 失败" -ForegroundColor Red
    Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor Red
    $failed++
}

# 查询 EKP 用户
Write-Host "[3] 查询 EKP 用户数量..." -NoNewline
try {
    $connString = "Server=$ekpServer,$ekpPort;Database=$ekpDatabase;User Id=$ekpUsername;Password=$ekpPassword;TrustServerCertificate=True;Connection Timeout=15;"
    
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    $connection.Open()
    
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM sys_org_person WHERE fd_is_available = 1"
    $count = $cmd.ExecuteScalar()
    
    $connection.Close()
    
    Write-Host " ✓ 找到 $count 个用户" -ForegroundColor Green
    $passed++
} catch {
    Write-Host " ✗ 失败" -ForegroundColor Red
    Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor Red
    $failed++
}

# 测试 Casdoor API 连接
Write-Host "[4] 测试 Casdoor API 连接..." -NoNewline
try {
    $url = "$casdoorEndpoint/api/get-organizations?owner=$casdoorOwner"
    $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 15
    
    Write-Host " ✓ 通过 (状态码: $($response.StatusCode))" -ForegroundColor Green
    $passed++
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host " ⊘ 跳过 (需要认证 - 正常)" -ForegroundColor Yellow
    } else {
        Write-Host " ✗ 失败" -ForegroundColor Red
        Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

# 测试 Casdoor 首页
Write-Host "[5] 测试 Casdoor 首页..." -NoNewline
try {
    $response = Invoke-WebRequest -Uri $casdoorEndpoint -Method Get -TimeoutSec 15
    
    Write-Host " ✓ 通过 (状态码: $($response.StatusCode))" -ForegroundColor Green
    $passed++
} catch {
    Write-Host " ✗ 失败" -ForegroundColor Red
    Write-Host "  错误: $($_.Exception.Message)" -ForegroundColor Red
    $failed++
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "测试完成" -ForegroundColor Cyan
Write-Host "通过: $passed 项" -ForegroundColor Green
Write-Host "失败: $failed 项" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host "========================================" -ForegroundColor Cyan

if ($failed -eq 0) {
    Write-Host ""
    Write-Host "✓ 所有关键连接测试通过！" -ForegroundColor Green
    Write-Host "  - EKP 数据库可以正常访问" -ForegroundColor Gray
    Write-Host "  - Casdoor 服务可以正常访问" -ForegroundColor Gray
    Write-Host ""
    Write-Host "现在可以使用 WPF 应用程序执行同步操作。" -ForegroundColor White
}
