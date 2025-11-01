# 设置SSH免密登录
$server = "172.16.10.110"
$user = "root"

Write-Host "=== 设置SSH免密登录 ===" -ForegroundColor Cyan

# 检查是否已有SSH密钥
if (!(Test-Path "$env:USERPROFILE\.ssh\id_rsa")) {
    Write-Host "生成SSH密钥..." -ForegroundColor Yellow
    ssh-keygen -t rsa -b 4096 -f "$env:USERPROFILE\.ssh\id_rsa" -N '""'
}

Write-Host "`n复制公钥到服务器..." -ForegroundColor Yellow
Write-Host "请输入服务器密码 (最后一次):" -ForegroundColor Green
type "$env:USERPROFILE\.ssh\id_rsa.pub" | ssh ${user}@${server} "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys && chmod 700 ~/.ssh"

Write-Host "`n测试SSH连接..." -ForegroundColor Yellow
ssh ${user}@${server} "echo 'SSH免密登录设置成功!'"

Write-Host "`n现在你可以无需密码登录服务器了!" -ForegroundColor Green
