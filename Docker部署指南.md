# 🐳 Docker部署完整指南

## 📋 前提条件

### Windows环境
1. **Docker Desktop for Windows**
   - 下载地址: https://www.docker.com/products/docker-desktop
   - 系统要求: Windows 10/11 Pro, Enterprise, or Education
   - 需要启用WSL2或Hyper-V

2. **配置要求**
   - 至少4GB RAM
   - 至少20GB可用磁盘空间

---

## 🚀 快速开始

### 方法1: 一键部署 (推荐)

1. **确保Docker Desktop已启动**
2. **运行部署脚本**

```powershell
# 在项目根目录执行
.\docker-quick-start.ps1
```

### 方法2: 完整部署脚本

```powershell
# 完整部署(首次部署)
.\deploy-docker.ps1

# 强制重新构建
.\deploy-docker.ps1 -Build

# 查看状态
.\deploy-docker.ps1 -Status

# 查看日志
.\deploy-docker.ps1 -Logs

# 停止容器
.\deploy-docker.ps1 -Stop

# 清理资源
.\deploy-docker.ps1 -Clean
```

### 方法3: 手动部署

```powershell
# 1. 进入项目目录
cd SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web

# 2. 构建镜像
docker-compose build

# 3. 启动容器
docker-compose up -d

# 4. 查看状态
docker ps

# 5. 查看日志
docker logs -f syncekp-casdoor-web
```

---

## 📁 Docker配置文件

### Dockerfile
```dockerfile
# 多阶段构建 - 优化镜像大小
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制并还原依赖
COPY ["SyncEkpToCasdoor.Web.csproj", "./"]
RUN dotnet restore

# 构建应用
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

# 运行时镜像
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 5233

ENV ASPNETCORE_URLS=http://+:5233
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TZ=Asia/Shanghai

# 健康检查工具
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SyncEkpToCasdoor.Web.dll"]
```

### docker-compose.yml
```yaml
version: '3.8'

services:
  syncekp-web:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: syncekp-casdoor-web
    restart: unless-stopped
    ports:
      - "5233:5233"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5233
      - TZ=Asia/Shanghai
    volumes:
      - ./logs:/app/logs
      - ./appsettings.json:/app/appsettings.json:ro
      - sync-state:/app
    networks:
      - app-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5233/login"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

volumes:
  sync-state:

networks:
  app-network:
    driver: bridge
```

---

## ⚙️ 配置说明

### 端口映射
- **容器端口**: 5233
- **主机端口**: 5233
- **访问地址**: http://localhost:5233

### 数据卷挂载
| 容器路径 | 主机路径 | 说明 |
|---------|---------|------|
| `/app/logs` | `./logs` | 应用日志 |
| `/app/appsettings.json` | `./appsettings.json` | 配置文件(只读) |
| `/app` | `sync-state` volume | 状态数据 |

### 环境变量
```yaml
ASPNETCORE_ENVIRONMENT=Production  # 生产环境
ASPNETCORE_URLS=http://+:5233     # 监听端口
TZ=Asia/Shanghai                   # 时区设置
```

---

## 🔍 部署后验证

### 1. 检查容器状态
```powershell
# 查看运行中的容器
docker ps

# 查看所有容器
docker ps -a

# 查看特定容器
docker ps --filter "name=syncekp-casdoor-web"
```

### 2. 查看日志
```powershell
# 实时日志
docker logs -f syncekp-casdoor-web

# 最近100行
docker logs --tail 100 syncekp-casdoor-web

# 带时间戳
docker logs -f --timestamps syncekp-casdoor-web
```

### 3. 健康检查
```powershell
# 查看健康状态
docker inspect --format='{{.State.Health.Status}}' syncekp-casdoor-web

# 测试访问
curl http://localhost:5233/login

# 或在浏览器中打开
start http://localhost:5233/login
```

### 4. 资源使用
```powershell
# 查看资源占用
docker stats syncekp-casdoor-web

# 查看容器详细信息
docker inspect syncekp-casdoor-web
```

---

## 🛠️ 容器管理

### 启动/停止/重启
```powershell
# 启动容器
docker start syncekp-casdoor-web

# 停止容器
docker stop syncekp-casdoor-web

# 重启容器
docker restart syncekp-casdoor-web

# 使用docker-compose
cd SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web
docker-compose stop
docker-compose start
docker-compose restart
```

### 进入容器
```powershell
# 进入容器bash
docker exec -it syncekp-casdoor-web /bin/bash

# 执行单个命令
docker exec syncekp-casdoor-web ls -la /app

# 查看配置文件
docker exec syncekp-casdoor-web cat /app/appsettings.json
```

### 更新应用
```powershell
# 方式1: 使用脚本
.\deploy-docker.ps1 -Build

# 方式2: 手动更新
cd SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

---

## 🐛 故障排查

### 问题1: 容器无法启动

**检查步骤:**
```powershell
# 1. 查看容器状态
docker ps -a --filter "name=syncekp-casdoor-web"

# 2. 查看详细日志
docker logs syncekp-casdoor-web

# 3. 检查端口占用
netstat -ano | findstr :5233

# 4. 重新构建
docker-compose build --no-cache
docker-compose up -d
```

### 问题2: 无法访问应用

**排查:**
```powershell
# 1. 确认容器运行
docker ps | findstr syncekp

# 2. 测试容器内部访问
docker exec syncekp-casdoor-web curl http://localhost:5233/login

# 3. 检查防火墙
# Windows防火墙可能阻止访问,需要允许端口5233

# 4. 检查配置文件
docker exec syncekp-casdoor-web cat /app/appsettings.json
```

### 问题3: 数据库连接失败

**解决方案:**
```powershell
# 1. 从容器测试数据库连接
docker exec syncekp-casdoor-web ping 数据库IP

# 2. 检查配置
docker exec syncekp-casdoor-web cat /app/appsettings.json | findstr EkpConnection

# 3. 使用host.docker.internal访问宿主机
# 在appsettings.json中,将localhost改为host.docker.internal
```

### 问题4: 日志文件未生成

**检查:**
```powershell
# 1. 查看日志目录
docker exec syncekp-casdoor-web ls -la /app/logs

# 2. 检查权限
docker exec syncekp-casdoor-web ls -ld /app/logs

# 3. 主机上查看
dir SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web\logs
```

---

## 🔒 安全建议

### 1. 配置文件安全
```powershell
# 使用环境变量代替明文密码
# 在docker-compose.yml中:
environment:
  - EkpConnection=${EKP_CONNECTION_STRING}
  - CasdoorAuth__ClientSecret=${CASDOOR_SECRET}
```

### 2. 网络隔离
```yaml
# 使用自定义网络
networks:
  app-network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.28.0.0/16
```

### 3. 资源限制
```yaml
# 在docker-compose.yml中添加:
deploy:
  resources:
    limits:
      cpus: '2'
      memory: 2G
    reservations:
      cpus: '1'
      memory: 1G
```

---

## 📊 性能优化

### 1. 镜像优化
```dockerfile
# 使用多阶段构建减小镜像大小
# 清理不必要的文件
RUN apt-get clean && rm -rf /var/lib/apt/lists/*
```

### 2. 日志管理
```yaml
# 在docker-compose.yml中配置日志限制:
logging:
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
```

### 3. 缓存优化
```powershell
# 使用BuildKit加速构建
$env:DOCKER_BUILDKIT=1
docker-compose build
```

---

## 🔄 备份和恢复

### 备份
```powershell
# 1. 备份配置文件
cp SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web\appsettings.json backup\appsettings.json.bak

# 2. 备份日志
xcopy SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web\logs backup\logs\ /E /I

# 3. 备份Docker卷
docker run --rm -v syncekptocasdoorweb_sync-state:/data -v ${PWD}:/backup busybox tar czf /backup/sync-state-backup.tar.gz /data
```

### 恢复
```powershell
# 1. 恢复配置
cp backup\appsettings.json.bak SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web\appsettings.json

# 2. 重新部署
.\deploy-docker.ps1 -Build
```

---

## 📈 监控

### 基础监控
```powershell
# 实时资源监控
docker stats syncekp-casdoor-web

# 健康检查历史
docker inspect --format='{{range .State.Health.Log}}{{.Start}} - {{.ExitCode}}{{println}}{{end}}' syncekp-casdoor-web
```

### 集成Prometheus(可选)
```yaml
# 添加metrics端点
# 安装Prometheus和Grafana进行高级监控
```

---

## ✅ 部署检查清单

部署前:
- [ ] Docker Desktop已安装并运行
- [ ] appsettings.json配置正确
- [ ] 端口5233未被占用
- [ ] 有足够的磁盘空间

部署中:
- [ ] 镜像构建成功
- [ ] 容器启动成功
- [ ] 健康检查通过
- [ ] 日志无错误

部署后:
- [ ] 可访问http://localhost:5233/login
- [ ] OAuth登录正常
- [ ] 数据库连接成功
- [ ] 日志正常输出
- [ ] 定时任务运行(如启用)

---

## 📞 获取帮助

### 常用命令速查
```powershell
# 快速部署
.\docker-quick-start.ps1

# 完整管理
.\deploy-docker.ps1          # 部署
.\deploy-docker.ps1 -Status  # 状态
.\deploy-docker.ps1 -Logs    # 日志
.\deploy-docker.ps1 -Stop    # 停止
.\deploy-docker.ps1 -Clean   # 清理

# Docker原生命令
docker ps                           # 查看容器
docker logs -f syncekp-casdoor-web # 查看日志
docker restart syncekp-casdoor-web # 重启
docker exec -it syncekp-casdoor-web /bin/bash  # 进入容器
```

### 访问地址
- **应用主页**: http://localhost:5233
- **登录页面**: http://localhost:5233/login
- **健康检查**: http://localhost:5233/login

---

**🎉 Docker部署完成后,访问 http://localhost:5233/login 开始使用!**
