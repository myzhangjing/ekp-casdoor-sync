# ==========================================
# Docker 快速部署 (一键启动)
# ==========================================

Write-Host "`n🐳 Docker 快速部署" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 检查Docker
try {
    docker version | Out-Null
} catch {
    Write-Host "❌ Docker未运行,请启动Docker Desktop" -ForegroundColor Red
    exit 1
}

# 进入项目目录
cd SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web

# 停止旧容器
Write-Host "[1/3] 停止旧容器..." -ForegroundColor Yellow
docker-compose down 2>$null

# 构建并启动
Write-Host "[2/3] 构建镜像..." -ForegroundColor Yellow
docker-compose build

Write-Host "[3/3] 启动容器..." -ForegroundColor Yellow
docker-compose up -d

# 等待启动
Start-Sleep -Seconds 5

# 检查状态
$status = docker ps --filter "name=syncekp-casdoor-web" --format "{{.Status}}"

if ($status) {
    Write-Host "`n✅ 部署成功!" -ForegroundColor Green
    Write-Host "   访问: http://localhost:5233/login" -ForegroundColor Cyan
    Write-Host "`n查看日志: docker logs -f syncekp-casdoor-web" -ForegroundColor Gray
} else {
    Write-Host "`n❌ 启动失败,查看日志:" -ForegroundColor Red
    docker logs syncekp-casdoor-web
}
