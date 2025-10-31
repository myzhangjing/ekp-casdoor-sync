#!/bin/bash
# ====================================
# 服务器端快速部署脚本
# Quick Deploy Script for Server
# ====================================

set -e  # 遇到错误立即退出

echo "========================================"
echo "  EKP-Casdoor-Sync Server Deployment"
echo "========================================"
echo ""

# 配置变量
REPO_URL="https://github.com/myzhangjing/ekp-casdoor-sync.git"
BRANCH="web-docker"
DEPLOY_DIR="$HOME/ekp-casdoor-sync"
PROJECT_DIR="$DEPLOY_DIR/SyncEkpToCasdoor_webdocker/SyncEkpToCasdoor.Web"
APP_NAME="ekp-casdoor-sync"
APP_PORT=5233

echo "[Step 1/7] 检查系统环境..."

# 检查Git
if ! command -v git &> /dev/null; then
    echo "❌ Git未安装，正在安装..."
    sudo apt-get update
    sudo apt-get install -y git
else
    echo "✓ Git已安装"
fi

# 检查.NET 8.0
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK未安装，正在安装..."
    
    # 添加Microsoft包源
    wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    
    # 安装.NET SDK 8.0
    sudo apt-get update
    sudo apt-get install -y dotnet-sdk-8.0
else
    DOTNET_VERSION=$(dotnet --version)
    echo "✓ .NET SDK已安装: $DOTNET_VERSION"
fi

echo ""
echo "[Step 2/7] 克隆/更新代码仓库..."

if [ -d "$DEPLOY_DIR" ]; then
    echo "目录已存在，拉取最新代码..."
    cd "$DEPLOY_DIR"
    
    # 保存当前配置文件
    if [ -f "$PROJECT_DIR/appsettings.json" ]; then
        echo "备份现有配置文件..."
        cp "$PROJECT_DIR/appsettings.json" "/tmp/appsettings.json.backup"
    fi
    
    # 拉取最新代码
    git fetch origin
    git checkout $BRANCH
    git pull origin $BRANCH
    
    # 恢复配置文件
    if [ -f "/tmp/appsettings.json.backup" ]; then
        echo "恢复配置文件..."
        cp "/tmp/appsettings.json.backup" "$PROJECT_DIR/appsettings.json"
    fi
else
    echo "克隆新仓库..."
    git clone -b $BRANCH $REPO_URL "$DEPLOY_DIR"
    cd "$DEPLOY_DIR"
fi

echo ""
echo "[Step 3/7] 停止现有服务..."

# 检查并停止systemd服务
if systemctl is-active --quiet $APP_NAME 2>/dev/null; then
    echo "停止systemd服务..."
    sudo systemctl stop $APP_NAME
fi

# 检查并停止端口占用的进程
if lsof -Pi :$APP_PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo "停止占用端口$APP_PORT的进程..."
    sudo kill -9 $(lsof -t -i:$APP_PORT) 2>/dev/null || true
fi

echo ""
echo "[Step 4/7] 编译发布应用..."

cd "$PROJECT_DIR"

# 清理旧的发布文件
if [ -d "bin/Release/net8.0/publish" ]; then
    rm -rf bin/Release/net8.0/publish
fi

# 发布应用
echo "正在编译发布 (Release模式)..."
dotnet publish -c Release -o bin/Release/net8.0/publish

if [ $? -ne 0 ]; then
    echo "❌ 编译失败!"
    exit 1
fi

echo "✓ 编译成功"

echo ""
echo "[Step 5/7] 配置系统服务..."

# 创建systemd服务文件
sudo tee /etc/systemd/system/$APP_NAME.service > /dev/null <<EOF
[Unit]
Description=EKP to Casdoor Sync Web Application
After=network.target

[Service]
Type=notify
WorkingDirectory=$PROJECT_DIR/bin/Release/net8.0/publish
ExecStart=/usr/bin/dotnet $PROJECT_DIR/bin/Release/net8.0/publish/SyncEkpToCasdoor.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME
User=$USER
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://0.0.0.0:$APP_PORT

[Install]
WantedBy=multi-user.target
EOF

echo "✓ 系统服务配置完成"

echo ""
echo "[Step 6/7] 启动服务..."

# 重新加载systemd配置
sudo systemctl daemon-reload

# 启用开机自启
sudo systemctl enable $APP_NAME

# 启动服务
sudo systemctl start $APP_NAME

# 等待启动
sleep 3

echo ""
echo "[Step 7/7] 检查服务状态..."

# 检查服务状态
if systemctl is-active --quiet $APP_NAME; then
    echo "✓ 服务启动成功"
    
    # 显示服务状态
    echo ""
    echo "服务状态:"
    sudo systemctl status $APP_NAME --no-pager -l
    
    echo ""
    echo "========================================"
    echo "  部署完成! 🎉"
    echo "========================================"
    echo ""
    echo "📋 部署信息:"
    echo "  - 服务名称: $APP_NAME"
    echo "  - 部署目录: $PROJECT_DIR"
    echo "  - 访问地址: http://localhost:$APP_PORT"
    echo "  - 配置文件: $PROJECT_DIR/appsettings.json"
    echo "  - 日志目录: $PROJECT_DIR/logs"
    echo ""
    echo "🔧 常用命令:"
    echo "  - 查看状态: sudo systemctl status $APP_NAME"
    echo "  - 重启服务: sudo systemctl restart $APP_NAME"
    echo "  - 停止服务: sudo systemctl stop $APP_NAME"
    echo "  - 查看日志: sudo journalctl -u $APP_NAME -f"
    echo "  - 查看应用日志: tail -f $PROJECT_DIR/logs/*.log"
    echo ""
    echo "🌐 外网访问 (需配置Nginx反向代理):"
    echo "  - 内网: http://$(hostname -I | awk '{print $1}'):$APP_PORT"
    echo "  - 外网: 需配置域名和Nginx"
    echo ""
    echo "⚙️  下一步:"
    echo "  1. 编辑配置文件: nano $PROJECT_DIR/appsettings.json"
    echo "  2. 配置数据库连接和Casdoor参数"
    echo "  3. 重启服务: sudo systemctl restart $APP_NAME"
    echo "  4. 访问应用: http://服务器IP:$APP_PORT/login"
    echo ""
    
    # 测试服务响应
    echo "正在测试服务响应..."
    sleep 2
    
    if curl -s -o /dev/null -w "%{http_code}" http://localhost:$APP_PORT/login | grep -q "200\|302"; then
        echo "✓ 服务响应正常"
    else
        echo "⚠️  服务可能需要几秒钟完全启动"
        echo "   请稍后访问: http://localhost:$APP_PORT/login"
    fi
    
else
    echo "❌ 服务启动失败"
    echo ""
    echo "错误日志:"
    sudo journalctl -u $APP_NAME -n 50 --no-pager
    exit 1
fi

echo ""
echo "========================================"
