# EKP 到 Casdoor 同步工具 - Web 版

## 概述

这是 EKP 到 Casdoor 同步工具的 Web 版本，使用 ASP.NET Core Blazor Server 构建，支持 Docker 容器化部署。相比 WPF 桌面版，Web 版本具有以下优势：

- ✅ **跨平台部署**：支持 Linux/Windows/macOS
- ✅ **Docker 容器化**：一键部署，易于管理
- ✅ **Web 界面访问**：无需客户端，浏览器即可管理
- ✅ **集中式管理**：适合服务器端运行，多人协作
- ✅ **自动化运维**：可配置定时任务，无人值守

## 快速开始

### 方式一：Docker Compose 部署（推荐）

1. **克隆仓库并切换到 web-docker 分支**
   ```bash
   git clone https://github.com/myzhangjing/ekp-casdoor-sync.git
   cd ekp-casdoor-sync
   git checkout web-docker
   cd SyncEkpToCasdoor_webdocker
   ```

2. **配置环境变量**
   
   编辑 `docker-compose.yml` 文件，修改以下配置：
   
   ```yaml
   environment:
     # EKP 数据库连接（必填）
     - EkpConnection=Server=your-server,11433;Database=ekp;User Id=xxzx;Password=your-password;Encrypt=False
     
     # Casdoor 配置（必填）
     - Casdoor__Endpoint=http://your-casdoor-server
     - Casdoor__ClientId=your-client-id
     - Casdoor__ClientSecret=your-client-secret
     - Casdoor__OrganizationName=your-org-name
   ```

3. **启动服务**
   ```bash
   docker-compose up -d
   ```

4. **访问 Web 界面**
   
   打开浏览器访问：http://localhost:8080

5. **查看日志**
   ```bash
   docker-compose logs -f sync-web
   ```

6. **停止服务**
   ```bash
   docker-compose down
   ```

### 方式二：本地开发运行

1. **安装依赖**
   
   - .NET 9.0 SDK
   - SQL Server 连接权限

2. **配置 appsettings.json**
   
   编辑 `SyncEkpToCasdoor.Web/appsettings.json`:
   
   ```json
   {
     "EkpConnection": "Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=your-password;Encrypt=False",
     "Casdoor": {
       "Endpoint": "http://sso.fzcsps.com",
       "ClientId": "aecd00a352e5c560ffe6",
       "ClientSecret": "your-secret",
       "OrganizationName": "fzswjtOrganization",
       "ApplicationName": "SyncEkpToCasdoor"
     }
   }
   ```

3. **运行项目**
   ```bash
   cd SyncEkpToCasdoor.Web
   dotnet run
   ```

4. **访问**
   
   浏览器打开 http://localhost:5000

## 功能说明

### 同步管理页面

访问 `/sync` 页面进行同步管理：

- **全量同步**：同步所有用户和组织数据
- **增量同步**：仅同步自上次同步以来变更的数据
- **应用优化视图**：更新数据库视图定义（修复部门识别问题）
- **同步状态**：查看最后同步时间和当前运行状态
- **用户查询**：按姓名或用户名查询用户同步情况

### API 接口

Web 版本同时提供 REST API，方便自动化调用：

```bash
# 执行全量同步
POST /api/sync/full

# 执行增量同步
POST /api/sync/incremental

# 应用优化视图
POST /api/sync/apply-views

# 查询用户
GET /api/sync/user?name=张三

# 获取同步状态
GET /api/sync/status
```

## 生产部署建议

### 1. 使用环境变量管理敏感信息

不要在 `docker-compose.yml` 中硬编码密码，使用 `.env` 文件：

```bash
# .env
EKP_PASSWORD=your-password
CASDOOR_CLIENT_SECRET=your-secret
```

修改 docker-compose.yml:
```yaml
environment:
  - EkpConnection=Server=npm.fzcsps.com,11433;Database=ekp;User Id=xxzx;Password=${EKP_PASSWORD};Encrypt=False
  - Casdoor__ClientSecret=${CASDOOR_CLIENT_SECRET}
```

### 2. 配置 HTTPS（推荐）

使用 Nginx 反向代理并配置 SSL 证书：

```nginx
server {
    listen 443 ssl;
    server_name sync.yourdomain.com;
    
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;
    
    location / {
        proxy_pass http://localhost:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 3. 配置定时同步

使用 cron 或系统定时任务定期执行增量同步：

```bash
# 每小时执行一次增量同步
0 * * * * curl -X POST http://localhost:8080/api/sync/incremental
```

### 4. 监控和日志

- 日志文件位置：`./logs` 目录
- 同步状态文件：`./data/sync_state.json`
- 使用 `docker-compose logs` 查看实时日志

## 架构说明

```
SyncEkpToCasdoor_webdocker/
├── Dockerfile                    # Docker 构建文件
├── docker-compose.yml            # Docker Compose 配置
├── README.md                     # 本文件
└── SyncEkpToCasdoor.Web/        # Web 项目
    ├── Program.cs                # 应用入口
    ├── appsettings.json          # 配置文件
    ├── Services/                 # 服务层
    │   ├── ISyncService.cs       # 同步服务接口
    │   └── SyncService.cs        # 同步服务实现
    └── Components/               # Blazor 组件
        └── Pages/
            └── Sync.razor        # 同步管理页面
```

## 技术栈

- **后端框架**：ASP.NET Core 9.0
- **前端框架**：Blazor Server
- **数据库访问**：Microsoft.Data.SqlClient
- **容器化**：Docker + Docker Compose
- **UI 框架**：Bootstrap 5

## 故障排查

### 1. 容器无法启动

```bash
# 查看容器日志
docker-compose logs sync-web

# 检查容器状态
docker-compose ps
```

### 2. 数据库连接失败

- 检查 EKP 数据库连接字符串是否正确
- 确认端口号（11433 而非默认的 1433）
- 确认 `Encrypt=False` 参数已设置
- 检查网络连通性：`telnet npm.fzcsps.com 11433`

### 3. Casdoor 认证失败

- 检查 Casdoor 端点 URL 是否可访问
- 验证 ClientId 和 ClientSecret 是否正确
- 确认 OrganizationName 和 ApplicationName 配置无误

## 从 WPF 版本迁移

Web 版本保留了原有的核心同步逻辑，但架构有所调整：

- **WPF UI** → **Blazor Web UI**
- **WPF 窗口** → **Web 页面**
- **本地文件配置** → **环境变量配置**
- **桌面应用** → **Web 服务**

迁移步骤：

1. 导出 WPF 版本的 `sync_state.json`
2. 复制到 Web 版本的 `./data/` 目录
3. 配置环境变量（对应原 WPF 配置）
4. 启动 Web 版本并验证

## 更新日志

### v2.0.0 (2025-01-31)
- ✨ 全新 Web 版本发布
- ✨ 支持 Docker 容器化部署
- ✨ Blazor Server UI 界面
- ✨ REST API 支持
- 🐛 修复视图部门识别优先级问题（继承自 WPF 修复）

## 许可证

MIT License

## 联系方式

- 项目地址：https://github.com/myzhangjing/ekp-casdoor-sync
- 问题反馈：https://github.com/myzhangjing/ekp-casdoor-sync/issues
