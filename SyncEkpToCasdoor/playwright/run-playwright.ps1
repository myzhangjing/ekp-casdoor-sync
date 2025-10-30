param(
    [string]$CasdoorUrl = 'https://sso.fzcsps.com',
    [string]$AdminUser = 'admin',
    [string]$AdminPass = '123'
)

Set-Location -Path (Join-Path $PSScriptRoot '')
Write-Host "切换到目录：" (Get-Location)

$env:CASDOOR_URL = $CasdoorUrl
$env:ADMIN_USER = $AdminUser
$env:ADMIN_PASS = $AdminPass

Write-Host "安装 Node 依赖（若已安装可跳过）：npm install"
npm install

Write-Host "安装 Playwright 浏览器二进制：npx playwright install"
npx playwright install

Write-Host "运行自动化脚本 create_app_users.js ..."
node create_app_users.js

Write-Host "运行结束。请检查目录下的截图和 trace.zip。"
