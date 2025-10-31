# ==========================================
# Docker 一键部署脚本 (Windows)
# Docker Deployment Script
# ==========================================

param(
    [switch]$Build,      # 强制重新构建镜像
    [switch]$Stop,       # 停止容器
    [switch]$Clean,      # 清理容器和镜像
    [switch]$Logs,       # 查看日志
    [switch]$Status      # 查看状态
)

$ErrorActionPreference = "Stop"
$ProjectDir = "SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web"
$ContainerName = "syncekp-casdoor-web"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Docker 部署工具" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 检查Docker是否运行
function Test-Docker {
    try {
        docker version | Out-Null
        return $true
    } catch {
        Write-Host "❌ Docker未运行或未安装" -ForegroundColor Red
        Write-Host "   请确保Docker Desktop已启动" -ForegroundColor Yellow
        return $false
    }
}

# 停止容器
function Stop-Container {
    Write-Host "[1/2] 停止现有容器..." -ForegroundColor Yellow
    
    if (docker ps -a --format "{{.Names}}" | Select-String -Pattern "^$ContainerName$") {
        docker stop $ContainerName 2>$null | Out-Null
        docker rm $ContainerName 2>$null | Out-Null
        Write-Host "✓ 容器已停止并移除" -ForegroundColor Green
    } else {
        Write-Host "✓ 无需停止(容器不存在)" -ForegroundColor Green
    }
}

# 清理镜像
function Clean-Images {
    Write-Host "[2/2] 清理Docker镜像..." -ForegroundColor Yellow
    
    $imageName = "syncekptocasdoorweb-syncekp-web"
    
    if (docker images --format "{{.Repository}}" | Select-String -Pattern $imageName) {
        docker rmi -f $imageName 2>$null | Out-Null
        Write-Host "✓ 镜像已删除" -ForegroundColor Green
    } else {
        Write-Host "✓ 无需清理(镜像不存在)" -ForegroundColor Green
    }
    
    # 清理悬空镜像
    $danglingImages = docker images -f "dangling=true" -q
    if ($danglingImages) {
        docker rmi $danglingImages 2>$null | Out-Null
        Write-Host "✓ 清理了悬空镜像" -ForegroundColor Green
    }
}

# 查看日志
function Show-Logs {
    if (docker ps --format "{{.Names}}" | Select-String -Pattern "^$ContainerName$") {
        Write-Host "`n实时日志 (Ctrl+C 退出):" -ForegroundColor Cyan
        docker logs -f --tail 50 $ContainerName
    } else {
        Write-Host "❌ 容器未运行" -ForegroundColor Red
    }
}

# 查看状态
function Show-Status {
    Write-Host "`n=== 容器状态 ===" -ForegroundColor Cyan
    docker ps -a --filter "name=$ContainerName" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    
    Write-Host "`n=== 资源使用 ===" -ForegroundColor Cyan
    if (docker ps --format "{{.Names}}" | Select-String -Pattern "^$ContainerName$") {
        docker stats --no-stream $ContainerName
    } else {
        Write-Host "容器未运行" -ForegroundColor Yellow
    }
    
    Write-Host "`n=== 最近日志 ===" -ForegroundColor Cyan
    if (docker ps --format "{{.Names}}" | Select-String -Pattern "^$ContainerName$") {
        docker logs --tail 10 $ContainerName
    } else {
        Write-Host "容器未运行" -ForegroundColor Yellow
    }
}

# 主部署流程
function Start-Deployment {
    Write-Host "[Step 1/6] 检查Docker环境..." -ForegroundColor Yellow
    
    if (-not (Test-Docker)) {
        exit 1
    }
    Write-Host "✓ Docker已就绪" -ForegroundColor Green
    
    Write-Host "`n[Step 2/6] 切换到项目目录..." -ForegroundColor Yellow
    
    if (-not (Test-Path $ProjectDir)) {
        Write-Host "❌ 项目目录不存在: $ProjectDir" -ForegroundColor Red
        exit 1
    }
    
    Push-Location $ProjectDir
    Write-Host "✓ 目录: $(Get-Location)" -ForegroundColor Green
    
    Write-Host "`n[Step 3/6] 检查配置文件..." -ForegroundColor Yellow
    
    if (-not (Test-Path "appsettings.json")) {
        Write-Host "❌ 缺少 appsettings.json" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    # 显示关键配置
    $config = Get-Content "appsettings.json" | ConvertFrom-Json
    Write-Host "✓ 配置文件存在" -ForegroundColor Green
    Write-Host "  - ClientId: $($config.CasdoorAuth.ClientId)" -ForegroundColor Gray
    Write-Host "  - Authority: $($config.CasdoorAuth.Authority)" -ForegroundColor Gray
    Write-Host "  - Target Companies: $($config.TargetCompanyIds)" -ForegroundColor Gray
    
    Write-Host "`n[Step 4/6] 停止现有容器..." -ForegroundColor Yellow
    
    if (docker ps -a --format "{{.Names}}" | Select-String -Pattern "^$ContainerName$") {
        docker-compose down
        Write-Host "✓ 已停止" -ForegroundColor Green
    } else {
        Write-Host "✓ 无需停止" -ForegroundColor Green
    }
    
    Write-Host "`n[Step 5/6] 构建Docker镜像..." -ForegroundColor Yellow
    
    if ($Build) {
        Write-Host "  强制重新构建..." -ForegroundColor Cyan
        docker-compose build --no-cache
    } else {
        docker-compose build
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 构建失败" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Write-Host "✓ 镜像构建成功" -ForegroundColor Green
    
    Write-Host "`n[Step 6/6] 启动容器..." -ForegroundColor Yellow
    
    docker-compose up -d
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ 启动失败" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Write-Host "✓ 容器已启动" -ForegroundColor Green
    
    # 等待服务启动
    Write-Host "`n等待服务启动..." -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    
    # 检查容器状态
    $containerStatus = docker inspect -f '{{.State.Status}}' $ContainerName 2>$null
    
    if ($containerStatus -eq "running") {
        Write-Host "✓ 容器运行正常" -ForegroundColor Green
        
        # 测试健康检查
        Write-Host "`n测试服务响应..." -ForegroundColor Cyan
        Start-Sleep -Seconds 3
        
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5233/login" -UseBasicParsing -TimeoutSec 10 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 302) {
                Write-Host "✓ 服务响应正常" -ForegroundColor Green
            }
        } catch {
            Write-Host "⚠ 服务可能需要几秒钟完全启动" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ 容器未能正常启动" -ForegroundColor Red
        Write-Host "`n查看错误日志:" -ForegroundColor Yellow
        docker logs --tail 30 $ContainerName
        Pop-Location
        exit 1
    }
    
    Pop-Location
    
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  部署完成! 🎉" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    Write-Host "📋 部署信息:" -ForegroundColor Yellow
    Write-Host "  容器名称: $ContainerName" -ForegroundColor White
    Write-Host "  访问地址: http://localhost:5233" -ForegroundColor White
    Write-Host "  登录页面: http://localhost:5233/login" -ForegroundColor White
    Write-Host ""
    Write-Host "🔧 管理命令:" -ForegroundColor Yellow
    Write-Host "  查看状态: .\deploy-docker.ps1 -Status" -ForegroundColor White
    Write-Host "  查看日志: .\deploy-docker.ps1 -Logs" -ForegroundColor White
    Write-Host "  停止容器: .\deploy-docker.ps1 -Stop" -ForegroundColor White
    Write-Host "  重新部署: .\deploy-docker.ps1" -ForegroundColor White
    Write-Host "  清理资源: .\deploy-docker.ps1 -Clean" -ForegroundColor White
    Write-Host ""
    Write-Host "📊 快速查看:" -ForegroundColor Yellow
    Write-Host "  docker ps | findstr $ContainerName" -ForegroundColor Gray
    Write-Host "  docker logs -f $ContainerName" -ForegroundColor Gray
    Write-Host "  docker exec -it $ContainerName /bin/bash" -ForegroundColor Gray
    Write-Host ""
}

# 主逻辑
if (-not (Test-Docker)) {
    exit 1
}

if ($Stop) {
    Write-Host "停止容器..." -ForegroundColor Yellow
    Push-Location $ProjectDir
    docker-compose down
    Pop-Location
    Write-Host "✓ 已停止" -ForegroundColor Green
    exit 0
}

if ($Clean) {
    Write-Host "清理Docker资源..." -ForegroundColor Yellow
    Stop-Container
    Clean-Images
    Write-Host "`n✓ 清理完成" -ForegroundColor Green
    exit 0
}

if ($Logs) {
    Show-Logs
    exit 0
}

if ($Status) {
    Show-Status
    exit 0
}

# 默认执行部署
Start-Deployment
