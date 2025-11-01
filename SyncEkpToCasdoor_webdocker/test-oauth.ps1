# OAuth 流程测试脚本
$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "Casdoor OAuth 流程测试"
Write-Host "========================================"
Write-Host ""

$baseUrl = "http://syn-ekp.fzcsps.com"
$casdoorUrl = "http://sso.fzcsps.com"

Write-Host "测试配置:"
Write-Host "  应用: $baseUrl"
Write-Host "  Casdoor: $casdoorUrl"
Write-Host ""

# 测试1: 应用服务
Write-Host "[1/5] 检查应用服务..."
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/" -Method GET -TimeoutSec 10 -UseBasicParsing
    Write-Host "  OK - 应用服务正常 (状态码: $($response.StatusCode))"
} catch {
    Write-Host "  ERROR - 应用无法访问: $($_.Exception.Message)"
    exit 1
}

# 测试2: Casdoor 服务
Write-Host "[2/5] 检查 Casdoor 服务..."
try {
    $response = Invoke-WebRequest -Uri "$casdoorUrl/" -Method GET -TimeoutSec 10 -UseBasicParsing
    Write-Host "  OK - Casdoor 服务正常"
} catch {
    Write-Host "  ERROR - Casdoor 无法访问: $($_.Exception.Message)"
    exit 1
}

# 测试3: 登录页面
Write-Host "[3/5] 检查登录页面..."
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/login" -Method GET -TimeoutSec 10 -UseBasicParsing
    Write-Host "  OK - 登录页面可访问 (状态码: $($response.StatusCode))"
} catch {
    Write-Host "  ERROR - 登录页面无法访问: $($_.Exception.Message)"
}

# 测试4: 回调端点
Write-Host "[4/5] 检查回调端点..."
try {
    $callbackUrl = "$baseUrl/callback"
    $response = Invoke-WebRequest -Uri $callbackUrl -Method GET -TimeoutSec 10 -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Write-Host "  OK - 回调端点可访问"
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        Write-Host "  OK - 回调端点正常 (重定向响应)"
    } else {
        Write-Host "  WARNING - 回调端点响应: $($_.Exception.Response.StatusCode)"
    }
}

# 测试5: 服务器日志
Write-Host "[5/5] 获取服务器日志..."
Write-Host ""
Write-Host "服务器日志 (最近 50 行):"
Write-Host "----------------------------------------"
try {
    ssh root@172.16.10.110 "docker logs --tail 50 syncekp-web 2>&1"
} catch {
    Write-Host "  ERROR - 无法获取日志: $($_.Exception.Message)"
}
Write-Host "----------------------------------------"

Write-Host ""
Write-Host "========================================"
Write-Host "测试完成"
Write-Host "========================================"
Write-Host ""
Write-Host "请在浏览器测试:"
Write-Host "1. 访问: $baseUrl"
Write-Host "2. 点击登录"
Write-Host "3. 使用 Casdoor 账号登录"
Write-Host "4. 查看是否能访问功能模块"
Write-Host ""
