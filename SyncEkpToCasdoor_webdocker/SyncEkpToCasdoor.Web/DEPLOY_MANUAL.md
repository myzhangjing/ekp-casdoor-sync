# 手动部署指南

由于 Windows OpenSSH 不支持密码认证，请按以下步骤手动部署：

## 📦 已创建的部署包
- **文件名**: deploy_20251031_165043.zip （或最新的 deploy_*.zip）
- **位置**: C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor_webdocker\SyncEkpToCasdoor.Web\

## 🚀 部署步骤

### 方法1：使用 WinSCP (推荐)
1. 下载安装 WinSCP: https://winscp.net/
2. 连接到服务器：
   - 主机名: 172.16.10.110
   - 用户名: root
   - 密码: fwater@163.com
3. 上传 deploy_*.zip 到服务器的 /tmp/ 目录
4. 打开 WinSCP 内置终端（Ctrl+T）执行以下命令：

```bash
cd /opt
mkdir -p syncekp-web
cd syncekp-web
unzip -o /tmp/deploy_20251031_165043.zip
rm /tmp/deploy_20251031_165043.zip

# 停止旧容器
docker stop syncekp-casdoor-web 2>/dev/null || true
docker rm syncekp-casdoor-web 2>/dev/null || true

# 构建并启动新容器
docker-compose build
docker-compose up -d

# 查看容器状态
docker ps | grep syncekp
docker logs syncekp-casdoor-web
```

### 方法2：使用 PuTTY + PSCP
1. 下载 PuTTY 套件: https://www.putty.org/
2. 使用 PSCP 上传文件（命令行）：
```cmd
pscp -pw fwater@163.com deploy_20251031_165043.zip root@172.16.10.110:/tmp/
```

3. 使用 PuTTY 连接服务器：
   - Host: 172.16.10.110
   - 用户名: root
   - 密码: fwater@163.com

4. 在 PuTTY 终端中执行上面方法1中的 bash 命令

### 方法3：使用 PowerShell (需要手动输入密码)
1. 上传文件（会提示输入密码）：
```powershell
scp -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL deploy_20251031_165043.zip root@172.16.10.110:/tmp/
```
密码: fwater@163.com

2. 连接并部署（会提示输入密码）：
```powershell
ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL root@172.16.10.110
```
密码: fwater@163.com

3. 在 SSH 会话中执行：
```bash
cd /opt
mkdir -p syncekp-web
cd syncekp-web
unzip -o /tmp/deploy_20251031_165043.zip
rm /tmp/deploy_20251031_165043.zip
docker stop syncekp-casdoor-web 2>/dev/null || true
docker rm syncekp-casdoor-web 2>/dev/null || true
docker-compose build
docker-compose up -d
docker ps | grep syncekp
```

## ✅ 验证部署

部署完成后，访问以下地址验证：

- **内网访问**: http://172.16.10.110:9000
- **外网访问**: http://syn-ekp.fzcsps.com:9000

应该能看到登录页面。

## 📋 查看日志

```bash
# 查看容器日志
docker logs -f syncekp-casdoor-web

# 查看容器状态
docker ps | grep syncekp

# 重启容器
docker restart syncekp-casdoor-web
```

## 🔧 故障排查

### 如果端口 9000 被占用
```bash
# 查看端口占用
netstat -tulpn | grep 9000

# 停止占用的容器
docker ps | grep 9000
docker stop <container_id>
```

### 如果容器启动失败
```bash
# 查看详细日志
docker logs syncekp-casdoor-web

# 检查 Docker Compose 配置
cd /opt/syncekp-web
docker-compose config

# 手动构建镜像
docker-compose build --no-cache
```

### 如果无法访问
1. 检查防火墙：`firewall-cmd --list-ports`
2. 开放端口：`firewall-cmd --add-port=9000/tcp --permanent && firewall-cmd --reload`
3. 检查 Docker 网络：`docker network ls`

## 📞 需要帮助？

如果遇到问题，请提供：
1. 错误日志：`docker logs syncekp-casdoor-web`
2. 容器状态：`docker ps -a | grep syncekp`
3. 端口占用：`netstat -tulpn | grep 9000`
