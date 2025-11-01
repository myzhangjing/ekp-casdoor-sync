# ==========================================
# 一键远程部署 (SSH)
# Quick Remote Deployment
# ==========================================

$SERVER = "172.16.10.110"
$USER = "root"
$PASS = "fzwater@163.com"

Write-Host "`n🚀 远程服务器一键部署" -ForegroundColor Cyan
Write-Host "目标: ${USER}@${SERVER}" -ForegroundColor Yellow
Write-Host ""

# 创建部署脚本
$deployScript = @'
#!/bin/bash
set -e

echo "========================================"
echo "  EKP-Casdoor-Sync 自动部署"
echo "========================================"

# 安装依赖
echo "[1/6] 检查环境..."
if ! command -v git &> /dev/null; then
    echo "安装Git..."
    apt-get update && apt-get install -y git
fi

if ! command -v dotnet &> /dev/null; then
    echo "安装.NET 8.0..."
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    apt-get update && apt-get install -y dotnet-sdk-8.0
fi

# 克隆代码
echo "[2/6] 获取代码..."
if [ -d "$HOME/ekp-casdoor-sync" ]; then
    cd $HOME/ekp-casdoor-sync
    git fetch origin
    git checkout web-docker
    git pull origin web-docker
else
    git clone -b web-docker https://github.com/myzhangjing/ekp-casdoor-sync.git $HOME/ekp-casdoor-sync
    cd $HOME/ekp-casdoor-sync
fi

# 编译
echo "[3/6] 编译应用..."
cd SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web
dotnet publish -c Release -o bin/Release/net8.0/publish

# 停止旧服务
echo "[4/6] 停止旧服务..."
systemctl stop ekp-casdoor-sync 2>/dev/null || true

# 配置服务
echo "[5/6] 配置系统服务..."
cat > /etc/systemd/system/ekp-casdoor-sync.service << 'EOF'
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
EOF

# 启动服务
echo "[6/6] 启动服务..."
systemctl daemon-reload
systemctl enable ekp-casdoor-sync
systemctl start ekp-casdoor-sync

sleep 3

echo ""
echo "========================================"
echo "  ✅ 部署完成!"
echo "========================================"
echo ""
echo "访问地址: http://172.16.10.110:5233/login"
echo ""
echo "管理命令:"
echo "  systemctl status ekp-casdoor-sync"
echo "  systemctl restart ekp-casdoor-sync"
echo "  journalctl -u ekp-casdoor-sync -f"
echo ""

# 显示状态
systemctl status ekp-casdoor-sync --no-pager
'@

# 保存到临时文件
$scriptFile = "$env:TEMP\deploy-remote.sh"
$deployScript | Out-File -FilePath $scriptFile -Encoding UTF8 -NoNewline

Write-Host "正在连接服务器..." -ForegroundColor Cyan
Write-Host ""

# 使用SSH连接(需要手动输入密码或使用SSH密钥)
Write-Host "提示: 请输入密码: $PASS" -ForegroundColor Yellow
Write-Host ""
Write-Host "执行以下命令:" -ForegroundColor Cyan
Write-Host "  1. scp ${scriptFile} ${USER}@${SERVER}:/tmp/deploy.sh" -ForegroundColor Gray
Write-Host "  2. ssh ${USER}@${SERVER} 'chmod +x /tmp/deploy.sh && /tmp/deploy.sh'" -ForegroundColor Gray
Write-Host ""

$confirm = Read-Host "是否自动执行? (Y/N)"

if ($confirm -eq "Y" -or $confirm -eq "y") {
    Write-Host ""
    Write-Host "[步骤1] 上传部署脚本..." -ForegroundColor Yellow
    scp $scriptFile ${USER}@${SERVER}:/tmp/deploy.sh
    
    Write-Host ""
    Write-Host "[步骤2] 执行部署..." -ForegroundColor Yellow
    ssh ${USER}@${SERVER} "chmod +x /tmp/deploy.sh; /tmp/deploy.sh"
    
    Write-Host ""
    Write-Host "✅ 完成!" -ForegroundColor Green
    Write-Host ""
    Write-Host "访问地址: http://172.16.10.110:5233/login" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "部署脚本已保存到: $scriptFile" -ForegroundColor Green
    Write-Host ""
    Write-Host "手动部署命令:" -ForegroundColor Yellow
    Write-Host "  scp $scriptFile ${USER}@${SERVER}:/tmp/deploy.sh" -ForegroundColor White
    Write-Host "  ssh ${USER}@${SERVER}" -ForegroundColor White
    Write-Host "  chmod +x /tmp/deploy.sh; /tmp/deploy.sh" -ForegroundColor White
}

Write-Host ""
