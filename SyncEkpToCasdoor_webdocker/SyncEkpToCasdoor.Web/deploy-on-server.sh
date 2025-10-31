#!/bin/bash
# 在服务器SSH终端中执行此脚本

echo "=========================================="
echo "开始部署 SyncEkpToCasdoor.Web"
echo "=========================================="
echo ""

cd /opt/syncekp-web

echo "[1/4] 停止旧容器..."
docker stop syncekp-casdoor-web 2>/dev/null || true
docker rm syncekp-casdoor-web 2>/dev/null || true
echo "✓ 旧容器已清理"
echo ""

echo "[2/4] 构建 Docker 镜像（需要5-10分钟）..."
docker compose build
if [ $? -ne 0 ]; then
    echo "✗ 构建失败！"
    exit 1
fi
echo "✓ 镜像构建成功"
echo ""

echo "[3/4] 启动容器..."
docker compose up -d
if [ $? -ne 0 ]; then
    echo "✗ 启动失败！"
    exit 1
fi
echo "✓ 容器启动成功"
echo ""

echo "[4/4] 等待应用启动..."
sleep 5

echo "检查容器状态..."
docker ps | grep syncekp

echo ""
echo "查看日志..."
docker logs --tail 30 syncekp-casdoor-web

echo ""
echo "=========================================="
echo "部署完成！"
echo "=========================================="
echo ""
echo "访问地址："
echo "  内网: http://172.16.10.110:9000"
echo "  外网: http://syn-ekp.fzcsps.com:9000"
echo ""
echo "查看实时日志："
echo "  docker logs -f syncekp-casdoor-web"
echo ""
