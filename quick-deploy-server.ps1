# ==========================================
# 一键远程部署到服务器
# ==========================================

$SERVER = "172.16.10.110"
$USER = "root"
$PASS = "fzwater@163.com"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  远程服务器一键部署" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "目标服务器: ${USER}@${SERVER}" -ForegroundColor Yellow
Write-Host "密码: $PASS" -ForegroundColor Yellow
Write-Host ""

# 创建部署脚本内容
$deployScript = @'
#!/bin/bash
set -e

echo "========================================"
echo "  EKP-Casdoor-Sync 自动部署"
echo "========================================"

# 安装Git
echo "[1/6] 检查Git..."
if ! command -v git &> /dev/null; then
    echo "安装Git..."
    apt-get update && apt-get install -y git
fi

# 安装.NET
echo "[2/6] 检查.NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo "安装.NET 8.0 SDK..."
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    apt-get update && apt-get install -y dotnet-sdk-8.0
fi

# 获取代码
echo "[3/6] 获取代码..."
if [ -d "$HOME/ekp-casdoor-sync" ]; then
    echo "更新现有代码..."
    cd $HOME/ekp-casdoor-sync
    git fetch origin
    git checkout web-docker
    git pull origin web-docker
else
    echo "克隆新代码..."
    git clone -b web-docker https://github.com/myzhangjing/ekp-casdoor-sync.git $HOME/ekp-casdoor-sync
    cd $HOME/ekp-casdoor-sync
fi

# 编译
echo "[4/6] 编译应用..."
cd SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web
dotnet publish -c Release -o bin/Release/net8.0/publish

# 停止服务
echo "[5/6] 停止旧服务..."
systemctl stop ekp-casdoor-sync 2>/dev/null || true

# 配置服务
echo "[6/6] 配置并启动服务..."
cat > /etc/systemd/system/ekp-casdoor-sync.service << 'SERVICEEOF'
[Unit]
Description=EKP to Casdoor Sync Web Application
After=network.target

[Service]
Type=notify
WorkingDirectory=/root/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web/bin/Release/net8.0/publish
ExecStart=/usr/bin/dotnet /root/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web/bin/Release/net8.0/publish/SyncEkpToCasdoor.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=ekp-casdoor-sync
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5233

[Install]
WantedBy=multi-user.target
SERVICEEOF

systemctl daemon-reload
systemctl enable ekp-casdoor-sync
systemctl start ekp-casdoor-sync

sleep 3

echo ""
echo "========================================"
echo "  部署完成!"
echo "========================================"
echo ""
echo "访问地址: http://172.16.10.110:5233/login"
echo ""
echo "常用命令:"
echo "  systemctl status ekp-casdoor-sync"
echo "  systemctl restart ekp-casdoor-sync"
echo "  journalctl -u ekp-casdoor-sync -f"
echo ""

systemctl status ekp-casdoor-sync --no-pager
'@

# 保存脚本
$scriptPath = "$env:TEMP\auto-deploy.sh"
$deployScript | Out-File -FilePath $scriptPath -Encoding UTF8

Write-Host "部署脚本已创建: $scriptPath" -ForegroundColor Green
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  开始部署" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "提示: SSH连接需要输入密码: $PASS" -ForegroundColor Yellow
Write-Host ""
Write-Host "按Enter开始部署..." -ForegroundColor Cyan
$null = Read-Host

Write-Host ""
Write-Host "[1/2] 上传部署脚本到服务器..." -ForegroundColor Yellow

# 使用scp上传
$uploadCmd = "scp `"$scriptPath`" ${USER}@${SERVER}:/tmp/auto-deploy.sh"
Write-Host "执行: $uploadCmd" -ForegroundColor Gray
Invoke-Expression $uploadCmd

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ 上传成功" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "[2/2] 执行部署脚本..." -ForegroundColor Yellow
    
    # 使用ssh执行
    $execCmd = "ssh ${USER}@${SERVER} `"chmod +x /tmp/auto-deploy.sh; /tmp/auto-deploy.sh`""
    Write-Host "执行: $execCmd" -ForegroundColor Gray
    Invoke-Expression $execCmd
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  部署完成!" -ForegroundColor Green
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    Write-Host "访问地址: http://172.16.10.110:5233/login" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "查看服务状态:" -ForegroundColor Yellow
    Write-Host "  ssh ${USER}@${SERVER} 'systemctl status ekp-casdoor-sync'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "查看日志:" -ForegroundColor Yellow
    Write-Host "  ssh ${USER}@${SERVER} 'journalctl -u ekp-casdoor-sync -f'" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "❌ 上传失败" -ForegroundColor Red
    Write-Host ""
    Write-Host "请手动执行以下命令:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. 上传脚本:" -ForegroundColor Cyan
    Write-Host "   scp `"$scriptPath`" ${USER}@${SERVER}:/tmp/auto-deploy.sh" -ForegroundColor White
    Write-Host ""
    Write-Host "2. SSH登录服务器:" -ForegroundColor Cyan
    Write-Host "   ssh ${USER}@${SERVER}" -ForegroundColor White
    Write-Host ""
    Write-Host "3. 执行部署:" -ForegroundColor Cyan
    Write-Host "   chmod +x /tmp/auto-deploy.sh" -ForegroundColor White
    Write-Host "   /tmp/auto-deploy.sh" -ForegroundColor White
    Write-Host ""
}
