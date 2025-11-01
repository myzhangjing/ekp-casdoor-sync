# 检查Docker部署状态
$server = "172.16.10.110"
$user = "root"

Write-Host "=== 检查Docker容器状态 ===" -ForegroundColor Cyan

# 检查容器列表
Write-Host "`n正在检查容器..." -ForegroundColor Yellow
ssh ${user}@${server} "docker ps -a | grep syncekp"

Write-Host "`n=== 检查容器日志 ===" -ForegroundColor Cyan
ssh ${user}@${server} "docker logs syncekp-web 2>&1 | tail -50"

Write-Host "`n=== 访问地址 ===" -ForegroundColor Green
Write-Host "http://${server}:9000/login" -ForegroundColor Green
