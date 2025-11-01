# OAuth 流程测试脚本
# 模拟完整的 Casdoor OAuth 登录流程

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Casdoor OAuth 流程测试工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 配置
$baseUrl = "http://syn-ekp.fzcsps.com"
$casdoorUrl = "http://sso.fzcsps.com"
$clientId = "aecd00a352e5c560ffe6"
$clientSecret = "8e7e0c2f3cb7d4aa81b0bd7c42c83e7c4dbb6e5e"
$redirectUri = "http://syn-ekp.fzcsps.com/callback"

Write-Host "测试配置:" -ForegroundColor Yellow
Write-Host "  应用地址: $baseUrl"
Write-Host "  Casdoor: $casdoorUrl"
Write-Host "  Client ID: $clientId"
Write-Host "  Callback: $redirectUri"
Write-Host ""

# 测试1: 检查应用是否运行
Write-Host "[1/6] 检查应用服务..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/" -Method GET -TimeoutSec 10 -UseBasicParsing
    Write-Host "  ✓ 应用服务正常运行 (状态码: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "  ✗ 应用服务无法访问: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 测试2: 检查 Casdoor 服务
Write-Host "[2/6] 检查 Casdoor 服务..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$casdoorUrl/.well-known/openid-configuration" -Method GET -TimeoutSec 10 -UseBasicParsing -ErrorAction SilentlyContinue
    Write-Host "  ✓ Casdoor 服务正常运行" -ForegroundColor Green
} catch {
    Write-Host "  ⚠ Casdoor OpenID 配置不可用,尝试基础连接..." -ForegroundColor Yellow
    try {
        $response = Invoke-WebRequest -Uri "$casdoorUrl/" -Method GET -TimeoutSec 10 -UseBasicParsing
        Write-Host "  ✓ Casdoor 基础服务正常" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ Casdoor 服务无法访问: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# 测试3: 检查登录页面
Write-Host "[3/6] 检查登录页面..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/login" -Method GET -TimeoutSec 10 -UseBasicParsing
    Write-Host "  ✓ 登录页面可访问 (状态码: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "  ✗ 登录页面无法访问: $($_.Exception.Message)" -ForegroundColor Red
}

# 测试4: 模拟 OAuth 授权请求
Write-Host "[4/6] 测试 OAuth 授权端点..." -ForegroundColor Yellow
$state = [System.Guid]::NewGuid().ToString()
$authUrl = "$casdoorUrl/login/oauth/authorize?client_id=$clientId&redirect_uri=$([System.Uri]::EscapeDataString($redirectUri))&response_type=code&scope=read&state=$state"
Write-Host "  授权 URL: $authUrl" -ForegroundColor Gray

try {
    # 创建 WebSession 来保持 Cookie
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $response = Invoke-WebRequest -Uri $authUrl -Method GET -TimeoutSec 10 -WebSession $session -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Write-Host "  ✓ 授权端点响应正常 (状态码: $($response.StatusCode))" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 302 -or $_.Exception.Response.StatusCode -eq 301) {
        Write-Host "  ✓ 授权端点正常重定向到登录页" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ 授权端点响应: $($_.Exception.Response.StatusCode)" -ForegroundColor Yellow
    }
}

# 测试5: 检查回调端点
Write-Host "[5/6] 测试回调端点..." -ForegroundColor Yellow
try {
    $testCallbackUrl = "$baseUrl/callback?code=test_code&amp;state=test_state"
    $response = Invoke-WebRequest -Uri $testCallbackUrl -Method GET -TimeoutSec 10 -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Write-Host "  ✓ 回调端点可访问" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        Write-Host "  ✓ 回调端点正常 (重定向响应)" -ForegroundColor Green
    } elseif ($_.Exception.Message -match "400|401|500") {
        Write-Host "  ✓ 回调端点存在 (预期的错误,因为是测试 code)" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ 回调端点响应: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# 测试6: 检查认证状态端点
Write-Host "[6/6] 检查应用认证配置..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/" -Method GET -TimeoutSec 10 -UseBasicParsing
    $content = $response.Content
    
    # 检查是否包含登录按钮或认证相关内容
    if ($content -match "login|登录|authentication") {
        Write-Host "  ✓ 应用包含认证相关内容" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ 未检测到明显的认证相关内容" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠ 无法检查应用内容: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "诊断建议:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查服务器日志
Write-Host "正在获取服务器最新日志..." -ForegroundColor Yellow
try {
    $sshCommand = "ssh root@172.16.10.110 `"docker logs --tail 50 syncekp-web 2>&1`""
    Write-Host ""
    Write-Host "服务器日志 (最近 50 行):" -ForegroundColor Cyan
    Write-Host "----------------------------------------" -ForegroundColor Gray
    Invoke-Expression $sshCommand
    Write-Host "----------------------------------------" -ForegroundColor Gray
} catch {
    Write-Host "  ⚠ 无法获取服务器日志: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "测试完成!" -ForegroundColor Green
Write-Host ""
Write-Host "请尝试以下操作:" -ForegroundColor Yellow
Write-Host "1. 在浏览器访问: $baseUrl" -ForegroundColor White
Write-Host "2. 点击登录按钮" -ForegroundColor White
Write-Host "3. 使用 Casdoor 账号登录" -ForegroundColor White
Write-Host "4. 查看是否成功跳转回应用" -ForegroundColor White
Write-Host ""
