# SyncEkpToCasdoor - 控制台同步工具

> EKP → Casdoor 组织与用户同步的核心引擎（.NET 8 控制台应用）

本项目是同步工具的命令行核心，适用于自动化脚本、定时任务或 CI/CD 流程。

**推荐阅读**：[仓库根目录 README](../README.md) 获取完整项目说明

---

## 📦 项目结构

```
SyncEkpToCasdoor/                # 控制台项目根目录
├── Program.cs                   # 入口与主同步逻辑
├── SyncEkpToCasdoor.csproj      # 项目文件
├── appsettings.json.example     # 配置示例
├── SyncEkpToCasdoor/            # 核心服务与模型
│   ├── Services/                # EKP、Casdoor、同步引擎服务
│   ├── Models/                  # 数据模型
│   └── Repositories/            # 数据访问层
└── SimpleCasdoorRepository.cs   # 简化的 Casdoor SDK 封装
```

---

## 🚀 快速开始

### 构建项目

```powershell
dotnet build SyncEkpToCasdoor.csproj -c Release
```

### 配置环境变量

```powershell
# 必需配置
$env:EKP_SQLSERVER_CONN = "Server=192.168.1.100;Database=ekp;User Id=sa;Password=****;TrustServerCertificate=True;"
$env:CASDOOR_ENDPOINT = "http://172.16.10.110:8000"
$env:CASDOOR_CLIENT_ID = "your-client-id"
$env:CASDOOR_CLIENT_SECRET = "your-client-secret"
$env:CASDOOR_DEFAULT_OWNER = "fzswjtOrganization"

# 可选配置
$env:EKP_USER_GROUP_VIEW = "vw_user_group_membership"   # 用户-组织视图
$env:SYNC_SINCE_UTC = "1970-01-01T00:00:00Z"           # 全量同步标志
```

### 运行同步

**方式一：使用统一脚本（推荐）**
```powershell
# 增量同步（从仓库根运行）
.\scripts\run-sync.ps1

# 全量同步
.\scripts\run-sync-full.ps1
```

**方式二：直接运行可执行文件**
```powershell
.\bin\Release\net8.0\SyncEkpToCasdoor.exe
```

---

## 🔧 核心功能

### 1. 组织同步
- 从 EKP 的 `vw_org_structure_sync` 视图读取组织架构
- 创建或更新 Casdoor 组织（Groups）
- 正确维护层级关系（`parentId`）
- 导出组织层级 CSV 供排查

### 2. 用户同步
- 从 EKP 的 `vw_casdoor_users_sync` 视图读取用户
- 创建或更新 Casdoor 用户
- 自动映射用户与组织关系
- 支持分页查询（避免大数据量超时）

### 3. 增量/全量模式
- **增量**：基于 `updated_at` 字段仅同步变更
- **全量**：设置 `SYNC_SINCE_UTC=1970-01-01` 强制同步所有数据

### 4. SQL 视图优化
内置命令可自动创建优化的 EKP 视图：

```powershell
# 应用所有优化视图
dotnet run --project SyncEkpToCasdoor.csproj -- apply-views
```

**视图列表**：
- `vw_org_structure_sync`：组织层级（移除递归 CTE，性能优化 120s → 0.36s）
- `vw_casdoor_users_sync`：用户信息（含 MD5 密码）
- `vw_user_group_membership`：用户-组织映射（`username`, `dept_id`）

---

## 📋 环境变量说明

| 变量名 | 必需 | 说明 | 示例 |
|--------|------|------|------|
| `EKP_SQLSERVER_CONN` | ✅ | EKP SQL Server 连接字符串 | `Server=...;Database=ekp;...` |
| `CASDOOR_ENDPOINT` | ✅ | Casdoor API 地址 | `http://172.16.10.110:8000` |
| `CASDOOR_CLIENT_ID` | ✅ | Casdoor 应用 Client ID | `abc123...` |
| `CASDOOR_CLIENT_SECRET` | ✅ | Casdoor 应用密钥 | `secret...` |
| `CASDOOR_DEFAULT_OWNER` | ✅ | Casdoor 组织 Owner | `fzswjtOrganization` |
| `EKP_USER_GROUP_VIEW` | ⬜ | 用户-组织视图名（默认内置） | `vw_user_group_membership` |
| `SYNC_SINCE_UTC` | ⬜ | 增量起点（ISO 格式） | `1970-01-01T00:00:00Z` |

**⚠️ 安全提示**：
- 配置通过环境变量注入，不要硬编码在代码或脚本中
- 不要将真实配置提交到版本控制系统

---

## �️ 诊断与排查命令

为快速定位“某个用户未同步/未入组”的问题，新增了以下只读诊断命令（需要设置 EKP 连接字符串）：

- 查看用户是否出现在用户视图中（支持用户名精确匹配或显示名模糊匹配）：

```powershell
# 先设置连接串
$env:EKP_SQLSERVER_CONN = "Server=...;Database=ekp;User Id=...;Password=...;TrustServerCertificate=True;"

# 关键字可以是登录名(=id/username)或中文显示名的一部分
dotnet run --project .\SyncEkpToCasdoor.csproj -- --peek-user 张璟
# 或
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --peek-user 张璟
```

- 查看某用户在成员关系视图中的部门列表（用于确认组装配来源）：

```powershell
dotnet run --project .\SyncEkpToCasdoor.csproj -- --peek-membership zhangjing
# 或
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --peek-membership zhangjing
```

输出将包含用户的 dept_id、company_name、affiliation、owner 以及更新时间，常见缺失原因会在无结果时给出提示。

提示：增量同步时，若用户的 updated_at 较早且未变化，将不会被本次同步覆盖；可将 `SYNC_SINCE_UTC` 置空或设为较早时间以强制全量同步。

---

## �🔍 字段映射

### Casdoor Group（组织）
| Casdoor 字段 | EKP 来源 | 说明 |
|-------------|---------|------|
| `name` | `fd_id` | 组织唯一标识 |
| `displayName` | `fd_name` | 组织中文名称 |
| `parentId` | 父组织的 `fd_id` | 层级关系（为空时设为 `owner`） |
| `owner` | 配置的 `CASDOOR_DEFAULT_OWNER` | 所属公司 |
| `key` | `dept_id` | EKP 部门 ID（用于溯源） |

### Casdoor User（用户）
| Casdoor 字段 | EKP 来源 | 说明 |
|-------------|---------|------|
| `name` | `fd_login_name` | 用户登录名 |
| `displayName` | `fd_name` | 用户中文名 |
| `email` | `fd_email` | 邮箱 |
| `phone` | `fd_mobile_no` | 手机号 |
| `password` | `password_md5` | MD5 密码（已加密） |
| `groups` | 关联视图 | 用户所属组织列表 |

---

## 📊 日志与输出

- **日志位置**：仓库根 `logs/` 目录（使用脚本时）
- **日志格式**：`sync_YYYYMMdd_HHmmss.log`
- **CSV 导出**：同步时自动导出组织层级到 CSV（供排查使用）

---

## 🐛 常见问题

### Q: 同步卡住或超时？
**A**: 
1. 检查 EKP 数据库视图是否优化（运行 `apply-views` 命令）
2. 为 `updated_at`、`id` 列添加索引
3. 使用分页参数避免一次加载过多数据

### Q: 组织层级不正确？
**A**: 
1. 检查 `vw_org_structure_sync` 视图是否正确返回 `parent_fd_id`
2. 使用归档脚本修复已存在的组织：
   - `docs/archive/scripts/fix-parentid-missing.ps1`
   - `docs/archive/scripts/update-parentid-from-csv.ps1`

### Q: 用户未同步到 Casdoor？
**A**: 
1. 确认 `vw_user_group_membership` 视图正常工作
2. 检查用户是否有关联的组织
3. 查看日志中的错误信息

---

## 📖 相关文档

- **仓库总文档**：[../README.md](../README.md)
- **WPF 界面使用**：[../docs/ui/README_WPF_UI.md](../docs/ui/README_WPF_UI.md)
- **脚本说明**：[../scripts/README.md](../scripts/README.md)
- **历史文档归档**：[../docs/archive/](../docs/archive/)

---

## 📝 许可证

MIT License - 详见 [LICENSE](../LICENSE)
