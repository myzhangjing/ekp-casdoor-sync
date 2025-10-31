# 快速启动脚本 - Web 版本（Windows PowerShell）

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "EKP 到 Casdoor 同步工具 - Web 版" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 检查 Docker 是否安装
$dockerInstalled = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerInstalled) {
    Write-Host "❌ 错误：未检测到 Docker，请先安装 Docker Desktop" -ForegroundColor Red
    exit 1
}

$dockerComposeInstalled = Get-Command docker-compose -ErrorAction SilentlyContinue
if (-not $dockerComposeInstalled) {
    Write-Host "❌ 错误：未检测到 Docker Compose，请先安装 Docker Compose" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Docker 环境检查通过" -ForegroundColor Green
Write-Host ""

# 检查配置文件
if (-not (Test-Path ".env")) {
    Write-Host "⚠️  警告：未找到 .env 文件，将使用 docker-compose.yml 中的默认配置" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "建议创建 .env 文件来管理敏感信息："
    Write-Host ""
    Write-Host "EKP_PASSWORD=your-password"
    Write-Host "CASDOOR_CLIENT_SECRET=your-secret"
    Write-Host ""
}

# 构建并启动服务
Write-Host "🚀 正在构建并启动服务..." -ForegroundColor Cyan
docker-compose up -d --build

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "✅ 服务启动成功！" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "📍 访问地址: http://localhost:8080" -ForegroundColor Cyan
    Write-Host "📍 同步管理: http://localhost:8080/sync" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "💡 常用命令:" -ForegroundColor Yellow
    Write-Host "  查看日志: docker-compose logs -f sync-web"
    Write-Host "  停止服务: docker-compose down"
    Write-Host "  重启服务: docker-compose restart"
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "❌ 服务启动失败，请检查日志：" -ForegroundColor Red
    Write-Host "  docker-compose logs sync-web"
    exit 1
}
