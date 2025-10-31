# 🐳 Docker部署准备就绪

## ✅ 更新完成

**时间**: 2025年10月31日  
**提交**: 44d4b03  
**状态**: 已同步到GitHub

---

## 📦 Docker部署文件清单

### 核心配置文件
- ✅ `Dockerfile` - 多阶段构建配置 (.NET 8.0)
- ✅ `docker-compose.yml` - 容器编排配置
- ✅ `.dockerignore` - 构建排除规则

### 部署脚本
- ✅ `deploy-docker.ps1` - 完整部署管理工具
- ✅ `docker-quick-start.ps1` - 一键快速启动
- ✅ `Docker部署指南.md` - 详细文档

---

## 🚀 快速开始

### 前提条件

1. **安装Docker Desktop**
   - Windows: https://www.docker.com/products/docker-desktop
   - 启动Docker Desktop确保运行

2. **检查Docker**
   ```powershell
   docker version
   ```

### 部署方式

#### 方式1: 一键快速启动 ⚡
```powershell
.\docker-quick-start.ps1
```

#### 方式2: 完整部署管理 🔧
```powershell
# 首次部署
.\deploy-docker.ps1

# 查看状态
.\deploy-docker.ps1 -Status

# 查看日志
.\deploy-docker.ps1 -Logs

# 停止容器
.\deploy-docker.ps1 -Stop

# 重新构建
.\deploy-docker.ps1 -Build
```

#### 方式3: 手动Docker命令 🛠️
```powershell
cd SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web
docker-compose build
docker-compose up -d
```

---

## 📋 Docker配置详情

### Dockerfile特性
```dockerfile
✅ 多阶段构建 - 减小镜像体积
✅ .NET 8.0运行时
✅ 生产环境优化
✅ 健康检查工具(curl)
✅ 时区设置(Asia/Shanghai)
```

### docker-compose配置
```yaml
✅ 容器名称: syncekp-casdoor-web
✅ 端口映射: 5233:5233
✅ 自动重启: unless-stopped
✅ 健康检查: 30秒间隔
✅ 日志挂载: ./logs
✅ 配置挂载: ./appsettings.json
✅ 数据持久化: sync-state volume
```

---

## 🌐 访问信息

部署成功后访问:

- **登录页面**: http://localhost:5233/login
- **应用主页**: http://localhost:5233
- **容器名称**: syncekp-casdoor-web

---

## 🔧 常用管理命令

### 容器管理
```powershell
# 查看运行状态
docker ps | findstr syncekp

# 查看日志
docker logs -f syncekp-casdoor-web

# 重启容器
docker restart syncekp-casdoor-web

# 停止容器
docker stop syncekp-casdoor-web

# 启动容器
docker start syncekp-casdoor-web

# 进入容器
docker exec -it syncekp-casdoor-web /bin/bash
```

### 使用docker-compose
```powershell
cd SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web

# 启动
docker-compose up -d

# 停止
docker-compose down

# 重启
docker-compose restart

# 查看日志
docker-compose logs -f

# 重新构建
docker-compose build --no-cache
docker-compose up -d
```

---

## 📊 健康检查

### 自动健康检查
容器已配置自动健康检查:
- **间隔**: 30秒
- **超时**: 10秒
- **重试**: 3次
- **启动等待**: 40秒

### 手动检查
```powershell
# 查看健康状态
docker inspect --format='{{.State.Health.Status}}' syncekp-casdoor-web

# 测试访问
curl http://localhost:5233/login

# 或在浏览器
start http://localhost:5233/login
```

---

## 🐛 故障排查

### 问题1: Docker未安装
```
❌ docker : 无法将"docker"项识别为 cmdlet
```
**解决**: 
1. 下载安装Docker Desktop: https://www.docker.com/products/docker-desktop
2. 启动Docker Desktop
3. 重新打开PowerShell

### 问题2: 容器启动失败
```powershell
# 查看错误日志
docker logs syncekp-casdoor-web

# 查看容器状态
docker ps -a | findstr syncekp

# 重新构建
.\deploy-docker.ps1 -Clean
.\deploy-docker.ps1 -Build
```

### 问题3: 端口冲突
```powershell
# 检查端口占用
netstat -ano | findstr :5233

# 修改docker-compose.yml中的端口映射
ports:
  - "5234:5233"  # 改为其他端口
```

### 问题4: 配置文件未生效
```powershell
# 检查配置挂载
docker exec syncekp-casdoor-web cat /app/appsettings.json

# 重新挂载
# 1. 修改本地appsettings.json
# 2. 重启容器
docker restart syncekp-casdoor-web
```

---

## 📈 性能监控

### 资源使用
```powershell
# 实时监控
docker stats syncekp-casdoor-web

# 查看详细信息
docker inspect syncekp-casdoor-web
```

### 日志管理
```powershell
# 查看应用日志
docker exec syncekp-casdoor-web ls -la /app/logs

# 查看最新日志
docker logs --tail 50 syncekp-casdoor-web

# 导出日志
docker logs syncekp-casdoor-web > app.log
```

---

## 🔄 更新部署

### 代码更新后
```powershell
# 1. 拉取最新代码
git pull origin web-docker

# 2. 重新部署
.\deploy-docker.ps1 -Build

# 或使用docker-compose
cd SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

---

## 🎯 部署检查清单

### 部署前
- [ ] Docker Desktop已安装
- [ ] Docker Desktop正在运行
- [ ] 端口5233未被占用
- [ ] appsettings.json已配置正确

### 部署中
- [ ] 镜像构建成功(无错误)
- [ ] 容器启动成功
- [ ] 健康检查通过

### 部署后验证
- [ ] `docker ps` 显示容器running
- [ ] 可以访问 http://localhost:5233/login
- [ ] 日志无严重错误
- [ ] OAuth登录功能正常

---

## 📚 相关文档

| 文档 | 说明 |
|------|------|
| `Docker部署指南.md` | 完整Docker部署文档 |
| `deploy-docker.ps1` | 部署管理脚本 |
| `docker-quick-start.ps1` | 快速启动脚本 |
| `服务器部署指南.md` | Linux服务器部署 |
| `快速部署到服务器.md` | 服务器快速开始 |

---

## 💡 高级用法

### 自定义配置
```yaml
# 在docker-compose.yml中修改:
environment:
  - ASPNETCORE_URLS=http://+:5233
  - CustomSetting=${YOUR_VALUE}

volumes:
  - ./custom-config.json:/app/custom-config.json
```

### 多容器部署
```yaml
# 添加数据库等其他服务
services:
  syncekp-web:
    # ... 现有配置
  
  database:
    image: mcr.microsoft.com/mssql/server:2019-latest
    # ... 数据库配置
```

### 网络配置
```yaml
networks:
  app-network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.28.0.0/16
```

---

## ✅ 部署成功标志

看到以下输出表示部署成功:

```
✅ 部署成功!
   访问: http://localhost:5233/login
```

验证方法:
1. ✅ 浏览器访问登录页面
2. ✅ 容器状态为healthy
3. ✅ 日志无错误
4. ✅ OAuth认证正常

---

## 🎉 总结

### 已完成
- ✅ Dockerfile配置(.NET 8.0)
- ✅ docker-compose编排
- ✅ 一键部署脚本
- ✅ 完整部署文档
- ✅ 健康检查配置
- ✅ 日志持久化
- ✅ 配置文件挂载

### 下一步
1. 安装Docker Desktop(如未安装)
2. 运行 `.\docker-quick-start.ps1`
3. 访问 http://localhost:5233/login
4. 开始使用!

---

**🐳 Docker部署已准备就绪,随时可以开始部署!**

**快速命令**: `.\docker-quick-start.ps1`
