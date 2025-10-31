#!/bin/bash
# 快速启动脚本 - Web 版本

echo "====================================="
echo "EKP 到 Casdoor 同步工具 - Web 版"
echo "====================================="
echo ""

# 检查 Docker 是否安装
if ! command -v docker &> /dev/null; then
    echo "❌ 错误：未检测到 Docker，请先安装 Docker"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "❌ 错误：未检测到 Docker Compose，请先安装 Docker Compose"
    exit 1
fi

echo "✅ Docker 环境检查通过"
echo ""

# 检查配置文件
if [ ! -f ".env" ]; then
    echo "⚠️  警告：未找到 .env 文件，将使用 docker-compose.yml 中的默认配置"
    echo ""
    echo "建议创建 .env 文件来管理敏感信息："
    echo ""
    echo "EKP_PASSWORD=your-password"
    echo "CASDOOR_CLIENT_SECRET=your-secret"
    echo ""
fi

# 构建并启动服务
echo "🚀 正在构建并启动服务..."
docker-compose up -d --build

if [ $? -eq 0 ]; then
    echo ""
    echo "====================================="
    echo "✅ 服务启动成功！"
    echo "====================================="
    echo ""
    echo "📍 访问地址: http://localhost:8080"
    echo "📍 同步管理: http://localhost:8080/sync"
    echo ""
    echo "💡 常用命令:"
    echo "  查看日志: docker-compose logs -f sync-web"
    echo "  停止服务: docker-compose down"
    echo "  重启服务: docker-compose restart"
    echo ""
else
    echo ""
    echo "❌ 服务启动失败，请检查日志："
    echo "  docker-compose logs sync-web"
    exit 1
fi
