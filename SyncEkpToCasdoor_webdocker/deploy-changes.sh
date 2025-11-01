#!/bin/bash
# 快速部署修改到服务器并重启
set -e

echo "======================================"
echo "  部署 AllowedUsers 动态验证功能"
echo "======================================"

DEPLOY_DIR="$HOME/ekp-casdoor-sync/SyncEkpToCasdoor_webdocker"
PROJECT_DIR="$DEPLOY_DIR/SyncEkpToCasdoor.Web"

cd "$DEPLOY_DIR"

echo ""
echo "[1/5] 拉取最新代码..."
git fetch origin
git checkout web-docker
git pull origin web-docker

echo ""
echo "[2/5] 停止现有容器..."
docker-compose down || true

echo ""
echo "[3/5] 重新构建镜像..."
docker-compose build --no-cache

echo ""
echo "[4/5] 启动新容器..."
docker-compose up -d

echo ""
echo "[5/5] 查看容器状态..."
docker-compose ps
echo ""
docker-compose logs --tail=50

echo ""
echo "======================================"
echo "✓ 部署完成!"
echo "======================================"
echo ""
echo "验证步骤:"
echo "1. 访问 http://syncas.fzcsps.com:8800/admin-login"
echo "2. 进入特权配置页面修改 AllowedUsers"
echo "3. 保存后,不在名单中的已登录用户会立即被拒绝访问"
echo ""
