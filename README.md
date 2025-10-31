# SyncEkpToCasdoor

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

> EKP → Casdoor 组织架构与用户数据同步工具（控制台 + WPF 界面）

## 📋 项目概述

本工具用于将 EKP 系统（金蝶协同办公平台）的组织架构、用户信息同步至 Casdoor 身份认证平台，支持：

- 🔄 **增量/全量同步**：按需同步组织与用户
- 🏢 **层级组织**：完整保留 EKP 组织树结构
- 👥 **用户关系**：自动映射用户与组织的从属关系
- 🖥️ **图形界面**：WPF 界面可视化配置、执行、查看
- 📊 **数据比对**：实时查看 EKP 与 Casdoor 数据差异
- 📦 **批量操作**：命令行工具支持脚本化部署

## 🚀 快速开始

### 前置要求

- .NET 8.0 SDK
- SQL Server（EKP 数据库）
- Casdoor 服务（已部署并获取 Client ID/Secret）
- Windows 10+ (WPF 界面) 或跨平台（控制台）

### 克隆仓库

```bash
git clone https://github.com/myzhangjing/ekp-casdoor-sync.git
cd ekp-casdoor-sync
```

### 方式一：使用 WPF 图形界面（推荐）

1. 构建并运行 UI：
   ```powershell
   dotnet build SyncEkpToCasdoor.sln -c Release
   cd SyncEkpToCasdoor\SyncEkpToCasdoor.UI\bin\Release\net8.0-windows
   .\SyncEkpToCasdoor.UI.exe
   ```

2. 在"配置管理"页面填写 EKP 与 Casdoor 连接信息
3. 测试连接，保存配置
4. 切换到"执行同步"页面，点击"增量同步"或"全量同步"
5. 在"数据查看"页面验证同步结果

📖 **详细 UI 使用指南**：[docs/ui/README_WPF_UI.md](docs/ui/README_WPF_UI.md)

### 方式二：使用命令行工具

1. 构建控制台程序：
   ```powershell
   dotnet build SyncEkpToCasdoor\SyncEkpToCasdoor.csproj -c Release
   ```

2. 配置环境变量（示例，请勿提交到仓库）：
   ```powershell
   $env:EKP_SQLSERVER_CONN = "Server=192.168.1.100,1433;Database=ekp;User Id=sa;Password=****;TrustServerCertificate=True;"
   $env:CASDOOR_ENDPOINT = "http://172.16.10.110:8000"
   $env:CASDOOR_CLIENT_ID = "your-client-id"
   $env:CASDOOR_CLIENT_SECRET = "your-client-secret"
   $env:CASDOOR_DEFAULT_OWNER = "fzswjtOrganization"
   $env:EKP_USER_GROUP_VIEW = "vw_user_group_membership"
   # 可选：全量同步
   $env:SYNC_SINCE_UTC = "1970-01-01T00:00:00Z"
   ```

3. 执行同步：
   ```powershell
   # 使用统一脚本（推荐）
   .\scripts\run-sync.ps1

   # 或直接运行可执行文件
   .\SyncEkpToCasdoor\bin\Release\net8.0\SyncEkpToCasdoor.exe
   ```

📖 **命令行工具详细说明**：[SyncEkpToCasdoor/README.md](SyncEkpToCasdoor/README.md)

## 📂 项目结构

```
SyncEkpToCasdoor/
├── SyncEkpToCasdoor/              # 控制台同步程序（.NET 8）
│   ├── Program.cs                 # 入口与核心逻辑
│   ├── Services/                  # 业务服务（EKP、Casdoor、同步引擎）
│   └── Models/                    # 数据模型
├── SyncEkpToCasdoor.UI/           # WPF 图形界面（.NET 8, Windows）
│   ├── ViewModels/                # MVVM 视图模型
│   ├── Services/                  # UI 专用服务
│   ├── Views/                     # 界面 XAML
│   └── Converters/                # 数据转换器
├── docs/                          # 统一文档目录
│   ├── README.md                  # 文档导航
│   ├── ui/                        # WPF 界面文档
│   └── archive/                   # 历史报告与归档
├── scripts/                       # 运行脚本
│   ├── run-sync.ps1               # 主同步脚本（增量）
│   └── run-sync-full.ps1          # 全量同步脚本
├── logs/                          # 日志输出目录（.gitignore）
├── SyncEkpToCasdoor.sln           # 解决方案文件（包含控制台 + UI）
├── .gitignore                     # Git 忽略规则
├── LICENSE                        # MIT 许可证
├── CHANGELOG.md                   # 变更日志
└── README.md                      # 本文件
```

## 📚 文档导航

- **快速开始**：[README.md](README.md)（本文件）
- **WPF 界面使用**：
  - [界面总览](docs/ui/README_WPF_UI.md)
  - [执行同步指南](docs/ui/执行同步使用说明.md)
  - [数据查看指南](docs/ui/数据查看使用说明.md)
  - [技术实现总结](docs/ui/数据查看模块总结.md)
- **控制台工具**：[SyncEkpToCasdoor/README.md](SyncEkpToCasdoor/README.md)
- **脚本使用**：[scripts/README.md](scripts/README.md)
- **监控指南**：
  - [如何检查定时同步](docs/monitoring/如何检查定时同步是否执行.md)
  - [监控完整方案](docs/monitoring/定时同步监控指南.md)
  - [双次执行问题修复](docs/monitoring/修复说明-两次操作问题.md)
- **变更记录**：[CHANGELOG.md](CHANGELOG.md)
- **贡献指南**：[CONTRIBUTING.md](CONTRIBUTING.md)

## 🔧 核心功能

### 1. 配置管理（WPF 界面）

- 可视化配置 EKP 数据库连接
- 可视化配置 Casdoor API 连接
- 一键测试连接可用性
- 安全存储配置文件（JSON）

### 2. 同步执行

- **增量同步**：仅同步自上次以来的变更（基于 `updated_at` 字段）
- **全量同步**：强制同步所有数据（设置 `SYNC_SINCE_UTC=1970-01-01`）
- **实时进度**：显示同步进度与统计
- **日志输出**：详细记录同步过程

### 3. 数据查看与比对

- 查看 EKP 原始数据（组织、用户）
- 查看 Casdoor 同步后数据
- 三分类比对：已同步 / 仅在 EKP / 仅在 Casdoor
- 搜索与筛选
- 导出 CSV 报表

### 4. SQL 视图优化

控制台工具内置命令可自动创建优化视图：

- `vw_org_structure_sync`：组织层级视图
- `vw_casdoor_users_sync`：用户信息视图（含 MD5 密码）
- `vw_user_group_membership`：用户-组织关系视图（`username`, `dept_id`）

```powershell
# 应用优化视图（需要 EKP 数据库写权限）
dotnet run --project SyncEkpToCasdoor -- apply-views
```

## ⚙️ 配置说明

### 环境变量（命令行模式）

| 变量名 | 必需 | 说明 | 示例 |
|--------|------|------|------|
| `EKP_SQLSERVER_CONN` | ✅ | EKP SQL Server 连接字符串 | `Server=192.168.1.100;Database=ekp;...` |
| `CASDOOR_ENDPOINT` | ✅ | Casdoor 服务地址 | `http://172.16.10.110:8000` |
| `CASDOOR_CLIENT_ID` | ✅ | Casdoor 应用 Client ID | `abc123...` |
| `CASDOOR_CLIENT_SECRET` | ✅ | Casdoor 应用密钥 | `secret...` |
| `CASDOOR_DEFAULT_OWNER` | ✅ | Casdoor 组织 Owner | `fzswjtOrganization` |
| `EKP_USER_GROUP_VIEW` | ⬜ | 用户-组织关系视图名 | `vw_user_group_membership` |
| `SYNC_SINCE_UTC` | ⬜ | 全量同步起点时间 | `1970-01-01T00:00:00Z` |

### 配置文件（WPF 界面模式）

界面会在运行目录生成 `sync_config.json`，包含上述所有配置。

**⚠️ 注意**：配置文件包含敏感信息，请勿提交到版本控制。

## 📊 定时同步与监控

### 配置定时任务

使用 Windows 任务计划程序设置每日自动同步：

```powershell
# 示例：每天凌晨 1:07 执行增量同步
# 1. 打开任务计划程序：taskschd.msc
# 2. 创建基本任务："EKP-Casdoor-DailySync"
# 3. 触发器：每日 01:07:00
# 4. 操作：PowerShell.exe -ExecutionPolicy Bypass -File "C:\path\to\scripts\run-sync.ps1"
# 5. 设置：以最高权限运行，无论用户是否登录都要运行
```

### 监控同步执行

**快速检查脚本**（一键查看同步状态）：

```powershell
# 检查最近同步状态
.\scripts\check-sync-status.ps1
```

**手动检查方法**：

1. **查看状态文件**：`sync_state.json`（记录最后执行时间）
2. **查看日志**：`logs/sync_YYYYMMDD_HHmmss.log`（每次执行生成新文件）
3. **检查任务历史**：任务计划程序 → 查看任务运行历史
4. **验证数据**：WPF 界面"数据查看"页面比对数据差异

📖 **详细监控指南**：
- [如何检查定时同步是否执行](docs/monitoring/如何检查定时同步是否执行.md)（快速参考）
- [定时同步监控指南](docs/monitoring/定时同步监控指南.md)（完整方案）

## 🧪 测试

```powershell
# 构建项目
dotnet build SyncEkpToCasdoor.sln -c Release

# （可选）运行单元测试
# dotnet test

# 验证同步脚本
.\scripts\run-sync.ps1
```

📋 **完整测试计划**：[docs/ui/测试计划.md](docs/ui/测试计划.md)

## 🐛 常见问题

### Q1: 同步进度卡在 2%？

**原因**：EKP 视图查询慢或超时  
**解决**：
1. 运行 `dotnet run -- apply-views` 应用优化视图
2. 在 EKP 数据库为 `updated_at` 和 `id` 列添加索引

### Q2: 用户未同步到 Casdoor？

**检查项**：
1. 在"数据查看"页面查看 EKP 用户列表
2. 使用"比对"模式查看差异
3. 检查日志是否有错误信息
4. 确认 `vw_user_group_membership` 视图正确返回数据

### Q3: WPF 界面连接测试失败？

**检查项**：
1. 确认网络连通性（ping EKP 服务器、Casdoor 服务器）
2. 检查防火墙规则
3. 验证 SQL Server 允许远程连接
4. 确认 Casdoor Client ID/Secret 正确

更多问题：[docs/ui/BUGFIX.md](docs/ui/BUGFIX.md)

## 📅 版本历史

- **v1.2.0** (2025-10-30)：数据查看与比对模块
- **v1.1.0** (2024-01-XX)：同步执行界面
- **v1.0.0** (2024-01-XX)：初始版本（控制台同步工具）

详细变更：[CHANGELOG.md](CHANGELOG.md)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

详见：[CONTRIBUTING.md](CONTRIBUTING.md)

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

## 👥 维护者

- [@myzhangjing](https://github.com/myzhangjing)

## 🙏 致谢

- [Casdoor](https://casdoor.org/) - 开源身份认证平台
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM 工具包
- 所有贡献者

---

**如有疑问，请提交 [Issue](https://github.com/myzhangjing/ekp-casdoor-sync/issues) 或参考 [文档目录](docs/README.md)**
