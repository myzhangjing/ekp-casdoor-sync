# 部署到服务器脚本
# 服务器信息
$SERVER_IP = "172.16.10.110"
$SERVER_USER = "root"
$SERVER_PASSWORD = "fwater@163.com"
$DEPLOY_PATH = "/opt/syncekp-web"
$APP_NAME = "syncekp-casdoor-web"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "开始部署 SyncEkpToCasdoor.Web 到服务器" -ForegroundColor Cyan
Write-Host "服务器: $SERVER_IP" -ForegroundColor Yellow
Write-Host "部署路径: $DEPLOY_PATH" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 创建部署包
Write-Host "[1/6] 创建部署包..." -ForegroundColor Green
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$packageName = "syncekp-web-$timestamp.zip"

# 压缩必要文件
$filesToZip = @(
    "Dockerfile",
    "docker-compose.yml",
    ".dockerignore",
    "appsettings.json",
    "appsettings.Production.json",
    "SyncEkpToCasdoor.Web.csproj",
    "Program.cs",
    "Components",
    "Controllers",
    "Models",
    "Services",
    "wwwroot"
)

Write-Host "压缩文件到 $packageName ..." -ForegroundColor Gray
Compress-Archive -Path $filesToZip -DestinationPath $packageName -Force
Write-Host "✓ 部署包创建完成" -ForegroundColor Green
Write-Host ""

# 2. 使用 SCP 上传文件
Write-Host "[2/6] 上传文件到服务器..." -ForegroundColor Green
Write-Host "提示: 可能需要手动输入密码: $SERVER_PASSWORD" -ForegroundColor Yellow

# 使用 pscp (PuTTY) 或 scp
try {
    # 尝试使用 scp (Windows 10+ 自带)
    Write-Host "上传 $packageName 到服务器..." -ForegroundColor Gray
    scp -o StrictHostKeyChecking=no $packageName ${SERVER_USER}@${SERVER_IP}:/tmp/
    
    if ($LASTEXITCODE -ne 0) {
        throw "SCP 上传失败"
    }
    Write-Host "✓ 文件上传完成" -ForegroundColor Green
} catch {
    Write-Host "✗ 上传失败: $_" -ForegroundColor Red
    Write-Host "请确保已安装 OpenSSH 客户端" -ForegroundColor Yellow
    Write-Host "或手动上传文件: $packageName 到服务器 /tmp/ 目录" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# 3. SSH 连接并执行部署命令
Write-Host "[3/6] 连接服务器并解压文件..." -ForegroundColor Green

$sshCommands = @"
# 创建部署目录
mkdir -p $DEPLOY_PATH
cd $DEPLOY_PATH

# 备份旧版本（如果存在）
if [ -d "old_backup" ]; then
    rm -rf old_backup
fi
if [ -f "Dockerfile" ]; then
    echo '备份旧版本...'
    mkdir -p old_backup
    mv -f Dockerfile docker-compose.yml Components Controllers Models Services wwwroot *.json *.csproj *.cs old_backup/ 2>/dev/null || true
fi

# 解压新版本
echo '解压新版本...'
unzip -o /tmp/$packageName -d $DEPLOY_PATH
rm -f /tmp/$packageName

echo '✓ 文件解压完成'
"@

Write-Host "执行远程命令..." -ForegroundColor Gray
ssh -o StrictHostKeyChecking=no ${SERVER_USER}@${SERVER_IP} $sshCommands

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ 远程命令执行失败" -ForegroundColor Red
    exit 1
}
Write-Host "✓ 文件解压完成" -ForegroundColor Green
Write-Host ""

# 4. 停止旧容器
Write-Host "[4/6] 停止旧容器..." -ForegroundColor Green
$stopCommands = @"
cd $DEPLOY_PATH

# 检查是否有运行的容器
if docker ps -a | grep -q '$APP_NAME'; then
    echo '停止并删除旧容器...'
    docker stop $APP_NAME 2>/dev/null || true
    docker rm $APP_NAME 2>/dev/null || true
    echo '✓ 旧容器已停止'
else
    echo '没有运行的旧容器'
fi

# 清理旧镜像（可选）
# docker rmi syncekp-web:latest 2>/dev/null || true
"@

ssh ${SERVER_USER}@${SERVER_IP} $stopCommands
Write-Host "✓ 旧容器清理完成" -ForegroundColor Green
Write-Host ""

# 5. 构建并启动新容器
Write-Host "[5/6] 构建并启动 Docker 容器..." -ForegroundColor Green
$buildCommands = @"
cd $DEPLOY_PATH

echo '构建 Docker 镜像...'
docker-compose build

echo '启动容器...'
docker-compose up -d

echo '等待容器启动...'
sleep 5

# 检查容器状态
if docker ps | grep -q '$APP_NAME'; then
    echo 'Container started successfully'
    docker ps | grep '$APP_NAME'
else
    echo 'Container start failed'
    echo 'Container logs:'
    docker logs $APP_NAME
    exit 1
fi
"@

ssh ${SERVER_USER}@${SERVER_IP} $buildCommands

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Docker 容器启动失败" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Docker 容器启动成功" -ForegroundColor Green
Write-Host ""

# 6. 验证部署
Write-Host "[6/6] 验证部署..." -ForegroundColor Green
Start-Sleep -Seconds 3

try {
    $response = Invoke-WebRequest -Uri "http://${SERVER_IP}:9000/login" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-Host "✓ 应用访问成功 (HTTP 200)" -ForegroundColor Green
    }
} catch {
    Write-Host "⚠ 应用可能还在启动中，请稍后访问: http://${SERVER_IP}:9000" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "部署完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "访问地址:" -ForegroundColor Yellow
Write-Host "  内网: http://${SERVER_IP}:9000" -ForegroundColor White
Write-Host "  外网: http://syn-ekp.fzcsps.com:9000" -ForegroundColor White
Write-Host ""
Write-Host "查看日志:" -ForegroundColor Yellow
Write-Host "  ssh ${SERVER_USER}@${SERVER_IP}" -ForegroundColor White
Write-Host "  cd $DEPLOY_PATH" -ForegroundColor White
Write-Host "  docker logs -f $APP_NAME" -ForegroundColor White
Write-Host ""
Write-Host "管理容器:" -ForegroundColor Yellow
Write-Host "  停止: docker stop $APP_NAME" -ForegroundColor White
Write-Host "  启动: docker start $APP_NAME" -ForegroundColor White
Write-Host "  重启: docker restart $APP_NAME" -ForegroundColor White
Write-Host ""

# 清理本地部署包
Remove-Item $packageName -Force
Write-Host "本地部署包已清理" -ForegroundColor Gray
