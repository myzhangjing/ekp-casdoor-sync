#!/bin/bash
# ==========================================
# 服务器自动部署脚本
# Auto Deployment Script
# ==========================================

set -e

SERVER_IP="172.16.10.110"
REPO_URL="https://github.com/myzhangjing/ekp-casdoor-sync.git"
BRANCH="web-docker"
APP_NAME="ekp-casdoor-sync"
APP_PORT=5233

echo "========================================"
echo "  EKP-Casdoor-Sync Auto Deployment"
echo "========================================"
echo ""

# [1/6] Check Git
echo "[1/6] Checking Git..."
if ! command -v git &> /dev/null; then
    echo "Installing Git..."
    apt-get update && apt-get install -y git
fi
echo "✓ Git ready"

# [2/6] Check .NET SDK
echo "[2/6] Checking .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo "Installing .NET 8.0 SDK..."
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    apt-get update && apt-get install -y dotnet-sdk-8.0
fi
echo "✓ .NET SDK ready"

# [3/6] Get Code
echo "[3/6] Getting code..."
if [ -d "$HOME/$APP_NAME" ]; then
    echo "Updating existing code..."
    cd $HOME/$APP_NAME
    git fetch origin
    git checkout $BRANCH
    git pull origin $BRANCH
else
    echo "Cloning repository..."
    git clone -b $BRANCH $REPO_URL $HOME/$APP_NAME
    cd $HOME/$APP_NAME
fi
echo "✓ Code ready"

# [4/6] Build Application
echo "[4/6] Building application..."
cd $HOME/$APP_NAME/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web
dotnet publish -c Release -o bin/Release/net8.0/publish
echo "✓ Build complete"

# [5/6] Stop Old Service
echo "[5/6] Stopping old service..."
systemctl stop $APP_NAME 2>/dev/null || true
echo "✓ Stopped"

# [6/6] Configure and Start Service
echo "[6/6] Configuring service..."

cat > /etc/systemd/system/$APP_NAME.service << 'EOF'
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

systemctl daemon-reload
systemctl enable $APP_NAME
systemctl start $APP_NAME

echo "✓ Service started"

sleep 3

echo ""
echo "========================================"
echo "  Deployment Complete!"
echo "========================================"
echo ""
echo "Access URL: http://$SERVER_IP:$APP_PORT/login"
echo ""
echo "Management Commands:"
echo "  systemctl status $APP_NAME"
echo "  systemctl restart $APP_NAME"
echo "  journalctl -u $APP_NAME -f"
echo ""

# Show status
systemctl status $APP_NAME --no-pager || true

echo ""
echo "Done!"
