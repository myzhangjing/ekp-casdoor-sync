#!/bin/bash
# 在服务器上执行的完整部署脚本
# 用法: 复制这些命令到 SSH 会话中执行

echo "========================================"
echo "SyncEkpToCasdoor.Web Deployment"
echo "========================================"
echo ""

cd /opt/syncekp-web || exit 1
pwd

echo ""
echo "[1/4] Checking Docker installation..."
docker --version
docker compose version

echo ""
echo "[2/4] Building Docker image..."
docker compose build

echo ""
echo "[3/4] Starting container..."
docker compose up -d

echo ""
echo "[4/4] Waiting for container to start..."
sleep 5

echo ""
echo "Container status:"
docker ps | grep syncekp

echo ""
echo "Recent logs:"
docker logs --tail 20 syncekp-casdoor-web

echo ""
echo "========================================"
echo "Deployment Status:"
echo "========================================"
docker ps | grep syncekp-casdoor-web && echo "✓ Container is running" || echo "✗ Container failed to start"

echo ""
echo "Access URLs:"
echo "  Internal: http://172.16.10.110:9000"
echo "  External: http://syn-ekp.fzcsps.com:9000"
echo ""
echo "Commands:"
echo "  View logs:    docker logs -f syncekp-casdoor-web"
echo "  Restart:      docker restart syncekp-casdoor-web"
echo "  Stop:         docker stop syncekp-casdoor-web"
echo "  Status:       docker ps | grep syncekp"
echo ""
