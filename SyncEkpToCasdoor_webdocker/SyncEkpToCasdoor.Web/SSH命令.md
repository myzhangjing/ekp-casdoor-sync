# 🚀 在SSH终端中执行以下命令

你现在已经连接到服务器了！请在SSH终端中复制粘贴以下命令：

## 一键部署命令（推荐）

```bash
cd /opt/syncekp-web && docker stop syncekp-casdoor-web 2>/dev/null || true && docker rm syncekp-casdoor-web 2>/dev/null || true && docker compose build && docker compose up -d && sleep 5 && docker ps | grep syncekp && docker logs --tail 30 syncekp-casdoor-web
```

## 或者分步执行

### 1. 进入部署目录
```bash
cd /opt/syncekp-web
```

### 2. 停止旧容器
```bash
docker stop syncekp-casdoor-web 2>/dev/null || true
docker rm syncekp-casdoor-web 2>/dev/null || true
```

### 3. 构建镜像（需要5-10分钟）
```bash
docker compose build
```

### 4. 启动容器
```bash
docker compose up -d
```

### 5. 查看状态
```bash
docker ps | grep syncekp
docker logs --tail 30 syncekp-casdoor-web
```

## 📊 验证部署

在浏览器中访问：
- http://172.16.10.110:9000
- http://syn-ekp.fzcsps.com:9000

应该能看到登录页面！

## 📋 常用命令

```bash
# 查看实时日志
docker logs -f syncekp-casdoor-web

# 重启容器
docker restart syncekp-casdoor-web

# 查看容器状态
docker ps -a | grep syncekp

# 停止容器
docker stop syncekp-casdoor-web

# 启动容器
docker start syncekp-casdoor-web
```

## ⚠️ 如果遇到问题

### 端口被占用
```bash
netstat -tulpn | grep 9000
```

### 查看详细日志
```bash
docker logs syncekp-casdoor-web
```

### 重新构建（清除缓存）
```bash
docker compose build --no-cache
```
