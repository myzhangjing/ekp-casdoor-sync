#!/bin/bash
# 快速部署和测试脚本

cd /opt/syncekp-web

echo "==> 1. 检查当前镜像"
docker images | grep syncekp

echo ""
echo "==> 2. 停止并删除旧容器"
docker stop syncekp-casdoor-web 2>/dev/null
docker rm syncekp-casdoor-web 2>/dev/null

echo ""
echo "==> 3. 删除旧镜像"
docker rmi syncekp-web-syncekp-web 2>/dev/null

echo ""
echo "==> 4. 重新构建（无缓存）"
docker compose build --no-cache

echo ""
echo "==> 5. 启动容器"
docker compose up -d

echo ""
echo "==> 6. 等待启动"
sleep 5

echo ""
echo "==> 7. 检查容器状态"
docker ps | grep syncekp

echo ""
echo "==> 8. 查看日志"
docker logs --tail 20 syncekp-casdoor-web

echo ""
echo "==> 9. 测试 /challenge 端点"
curl -I http://localhost:9000/challenge 2>&1 | grep -E "HTTP|Location"

echo ""
echo "完成！"
