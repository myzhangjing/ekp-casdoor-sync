# 测试登录流程
Write-Host "开始测试登录流程..." -ForegroundColor Green

# 1. 测试首页是否可访问
Write-Host "`n1. 测试首页..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5233" -Method Get -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Write-Host "首页状态码: $($response.StatusCode)" -ForegroundColor Cyan
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        Write-Host "首页重定向到: $($_.Exception.Response.Headers.Location)" -ForegroundColor Cyan
    } else {
        Write-Host "首页访问失败: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# 2. 测试 /challenge 端点
Write-Host "`n2. 测试 /challenge 端点..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5233/challenge" -Method Get -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Write-Host "Challenge 状态码: $($response.StatusCode)" -ForegroundColor Red
    Write-Host "返回内容长度: $($response.Content.Length)" -ForegroundColor Red
    Write-Host "前200个字符: $($response.Content.Substring(0, [Math]::Min(200, $response.Content.Length)))" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        $location = $_.Exception.Response.Headers.Location
        Write-Host "✓ Challenge 正确重定向到: $location" -ForegroundColor Green
        if ($location -like "*sso.fzcsps.com*") {
            Write-Host "✓ 重定向地址包含 Casdoor SSO 域名" -ForegroundColor Green
        } else {
            Write-Host "✗ 重定向地址不正确,不包含 Casdoor SSO 域名" -ForegroundColor Red
        }
    } else {
        Write-Host "Challenge 访问失败: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# 3. 测试 /login 页面
Write-Host "`n3. 测试 /login 页面..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5233/login" -Method Get -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Write-Host "Login 页面状态码: $($response.StatusCode)" -ForegroundColor Cyan
    if ($response.Content -match "Casdoor") {
        Write-Host "✓ Login 页面包含 Casdoor 相关内容" -ForegroundColor Green
    }
} catch {
    Write-Host "Login 页面访问失败: $($_.Exception.Message)" -ForegroundColor Red
}

# 4. 检查日志中的 Challenge 调用
Write-Host "`n4. 检查应用日志..." -ForegroundColor Yellow
Start-Sleep -Seconds 2
Write-Host "请查看应用终端输出,确认是否有 'Challenge 页面被访问' 的日志" -ForegroundColor Cyan

Write-Host "`n测试完成!" -ForegroundColor Green
Write-Host "如果 /challenge 返回状态码 200 而不是 302,说明 Blazor 仍在拦截路由" -ForegroundColor Yellow
