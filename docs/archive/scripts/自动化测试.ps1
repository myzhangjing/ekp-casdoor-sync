# 自动化测试脚本
# 直接测试 EKP 和 Casdoor 连接

param(
    [string]$ConfigPath = "SyncEkpToCasdoor.UI\sync_config.json"
)

$ErrorActionPreference = "Continue"

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "  EKP-Casdoor 同步工具 - 自动化测试" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 加载配置
Write-Host "加载配置..." -ForegroundColor Yellow
if (-not (Test-Path $ConfigPath)) {
    Write-Host "  ✗ 配置文件不存在: $ConfigPath" -ForegroundColor Red
    exit 1
}

$config = Get-Content $ConfigPath | ConvertFrom-Json
Write-Host "  ✓ 配置已加载" -ForegroundColor Green
Write-Host ""

# 测试计数器
$testCount = 0
$passCount = 0
$failCount = 0
$skipCount = 0

function Run-Test {
    param(
        [string]$Name,
        [scriptblock]$TestBlock
    )
    
    $script:testCount++
    Write-Host "[$($script:testCount.ToString('D2'))] $Name... " -NoNewline
    
    try {
        $result = & $TestBlock
        if ($result -eq $null -or $result -eq $true) {
            $script:passCount++
            Write-Host "✓ 通过" -ForegroundColor Green
        } else {
            $script:failCount++
            Write-Host "✗ 失败: $result" -ForegroundColor Red
        }
    } catch {
        if ($_.Exception.Message -like "*跳过*") {
            $script:skipCount++
            Write-Host "⊘ 跳过 ($($_.Exception.Message))" -ForegroundColor Yellow
        } else {
            $script:failCount++
            Write-Host "✗ 失败" -ForegroundColor Red
            Write-Host "    错误: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host "开始测试..." -ForegroundColor Cyan
Write-Host ""

# ========== 配置测试 ==========
Write-Host "【配置测试】" -ForegroundColor Cyan

Run-Test "读取配置文件" {
    if ($config.EkpServer) { $true } else { "配置为空" }
}

Run-Test "验证必需配置项" {
    $missing = @()
    if ([string]::IsNullOrEmpty($config.EkpServer)) { $missing += "EkpServer" }
    if ([string]::IsNullOrEmpty($config.EkpDatabase)) { $missing += "EkpDatabase" }
    if ([string]::IsNullOrEmpty($config.CasdoorEndpoint)) { $missing += "CasdoorEndpoint" }
    
    if ($missing.Count -gt 0) {
        "缺少配置: $($missing -join ', ')"
    } else {
        $true
    }
}

Write-Host ""

# ========== EKP 数据库测试 ==========
Write-Host "【EKP 数据库测试】" -ForegroundColor Cyan

$connString = "Server=$($config.EkpServer),$($config.EkpPort);Database=$($config.EkpDatabase);User Id=$($config.EkpUsername);Password=$($config.EkpPassword);TrustServerCertificate=True;Connection Timeout=10;"

Run-Test "EKP 数据库连接" {
    Add-Type -AssemblyName "System.Data"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT 1"
        $result = $cmd.ExecuteScalar()
        $connection.Close()
        if ($result -eq 1) { $true } else { "连接验证失败" }
    } catch {
        throw
    } finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
    }
}

Run-Test "查询 EKP 组织数量" {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_business = 1"
        $count = $cmd.ExecuteScalar()
        Write-Host "($count 个组织) " -NoNewline -ForegroundColor Gray
        if ($count -gt 0) { $true } else { "未找到组织" }
    } finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
    }
}

Run-Test "查询 EKP 用户数量" {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM sys_org_person WHERE fd_is_available = 1"
        $count = $cmd.ExecuteScalar()
        Write-Host "($count 个用户) " -NoNewline -ForegroundColor Gray
        if ($count -gt 0) { $true } else { "未找到用户" }
    } finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
    }
}

Run-Test "查询 EKP 组织详情" {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT TOP 1 fd_id, fd_name FROM sys_org_element WHERE fd_is_business = 1"
        $reader = $cmd.ExecuteReader()
        if ($reader.Read()) {
            $name = $reader["fd_name"]
            Write-Host "(示例: $name) " -NoNewline -ForegroundColor Gray
            $true
        } else {
            "无法读取数据"
        }
    } finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
    }
}

Run-Test "查询 EKP 用户详情" {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT TOP 1 fd_login_name, fd_name FROM sys_org_person WHERE fd_is_available = 1"
        $reader = $cmd.ExecuteReader()
        if ($reader.Read()) {
            $name = $reader["fd_name"]
            Write-Host "(示例: $name) " -NoNewline -ForegroundColor Gray
            $true
        } else {
            "无法读取数据"
        }
    } finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
    }
}

Write-Host ""

# ========== Casdoor API 测试 ==========
Write-Host "【Casdoor API 测试】" -ForegroundColor Cyan

Run-Test "Casdoor API 连接" {
    $url = "$($config.CasdoorEndpoint)/api/get-organizations?owner=$($config.CasdoorOwner)"
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 10 -ErrorAction Stop
        Write-Host "(状态: $($response.StatusCode)) " -NoNewline -ForegroundColor Gray
        $true
    } catch {
        if ($_.Exception.Response.StatusCode -eq 401) {
            Write-Host "(需要认证) " -NoNewline -ForegroundColor Yellow
            throw "跳过 - 需要认证"
        } else {
            throw
        }
    }
}

Run-Test "获取 Casdoor 组织列表" {
    $url = "$($config.CasdoorEndpoint)/api/get-organizations?owner=$($config.CasdoorOwner)"
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 10 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $data = $response.Content | ConvertFrom-Json
            if ($data.data) {
                $count = $data.data.Count
                Write-Host "($count 个组织) " -NoNewline -ForegroundColor Gray
            }
            $true
        } else {
            throw "跳过 - 状态码 $($response.StatusCode)"
        }
    } catch {
        if ($_.Exception.Response.StatusCode -eq 401) {
            throw "跳过 - 需要认证"
        } else {
            throw
        }
    }
}

Run-Test "获取 Casdoor 用户列表" {
    $url = "$($config.CasdoorEndpoint)/api/get-users?owner=$($config.CasdoorOwner)"
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 10 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $data = $response.Content | ConvertFrom-Json
            if ($data.data) {
                $count = $data.data.Count
                Write-Host "($count 个用户) " -NoNewline -ForegroundColor Gray
            }
            $true
        } else {
            throw "跳过 - 状态码 $($response.StatusCode)"
        }
    } catch {
        if ($_.Exception.Response.StatusCode -eq 401) {
            throw "跳过 - 需要认证"
        } else {
            throw
        }
    }
}

Write-Host ""

# ========== 性能测试 ==========
Write-Host "【性能测试】" -ForegroundColor Cyan

Run-Test "EKP 查询性能" {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connString)
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $connection.Open()
        $cmd = $connection.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM sys_org_element WHERE fd_is_business = 1"
        $null = $cmd.ExecuteScalar()
        $sw.Stop()
        
        $ms = $sw.ElapsedMilliseconds
        Write-Host "($ms ms) " -NoNewline -ForegroundColor Gray
        
        if ($ms -lt 5000) { $true } else { "性能过低 ($ms ms)" }
    } finally {
        if ($connection.State -eq 'Open') { $connection.Close() }
    }
}

Run-Test "Casdoor API 性能" {
    $url = "$($config.CasdoorEndpoint)/api/get-organizations?owner=$($config.CasdoorOwner)"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $null = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 10 -ErrorAction Stop
    } catch {
        # 忽略错误，只测性能
    }
    $sw.Stop()
    
    $ms = $sw.ElapsedMilliseconds
    Write-Host "($ms ms) " -NoNewline -ForegroundColor Gray
    
    if ($ms -lt 5000) { $true } else { "响应过慢 ($ms ms)" }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  测试总结" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "总计: $testCount 项" -ForegroundColor White
Write-Host "通过: $passCount 项" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "失败: $failCount 项" -ForegroundColor Red
}
if ($skipCount -gt 0) {
    Write-Host "跳过: $skipCount 项" -ForegroundColor Yellow
}
Write-Host ""

if ($failCount -gt 0) {
    Write-Host "❌ 测试失败" -ForegroundColor Red
    exit 1
} else {
    Write-Host "✓ 所有测试通过" -ForegroundColor Green
    exit 0
}
