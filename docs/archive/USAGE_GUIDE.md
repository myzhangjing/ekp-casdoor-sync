# EKP-Casdoor 同步系统使用指南

## 📋 目录
1. [快速开始](#快速开始)
2. [环境配置](#环境配置)
3. [日常使用](#日常使用)
4. [故障排查工具](#故障排查工具)
5. [常见问题](#常见问题)

---

## 🚀 快速开始

### 前置要求
- .NET 8.0 SDK
- 能访问 EKP 的 SQL Server 数据库
- 能访问 Casdoor API 接口
- PowerShell 5.1+ (Windows) 或 PowerShell Core (跨平台)

### 首次安装

1. **克隆/下载项目到本地**
   ```powershell
   cd C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor
   ```

2. **编译项目**
   ```powershell
   dotnet build -c Release
   ```

3. **配置环境变量**（见下节）

4. **首次全量同步**
   ```powershell
   # 删除状态文件，强制全量同步
   Remove-Item sync_state.json -Force -ErrorAction SilentlyContinue
   .\bin\Release\net8.0\SyncEkpToCasdoor.exe
   ```

---

## ⚙️ 环境配置

### 必需的环境变量

在 PowerShell 中设置（仅当前会话有效）：

```powershell
# EKP 数据库连接
$env:EKP_SQLSERVER_CONN = "Server=172.16.10.110,1433;Database=ekp;User Id=sa;Password=your_password;TrustServerCertificate=True;"

# Casdoor 接口配置
$env:CASDOOR_ENDPOINT = "http://sso.fzcsps.com"
$env:CASDOOR_CLIENT_ID = "aecd00a352e5c560ffe6"
$env:CASDOOR_CLIENT_SECRET = "4402518b20dd191b8b48d6240bc786a4f847899a"

# Casdoor 组织 Owner
$env:CASDOOR_DEFAULT_OWNER = "fzswjtOrganization"

# 用户-组织关系视图（可选，默认为 vw_user_group_membership）
$env:EKP_USER_GROUP_VIEW = "vw_user_group_membership"
```

### 可选环境变量

```powershell
# 强制全量同步（覆盖状态文件中的时间戳）
$env:SYNC_SINCE_UTC = "1970-01-01T00:00:00Z"

# Enforcer 列表（多个用逗号分隔）
$env:CASDOOR_ENFORCERS = "built-in/user-enforcer-built-in,fzswjtOrganization/my-enforcer"

# 同步状态文件路径（默认为 sync_state.json）
$env:SYNC_STATE_FILE = "sync_state.json"
```

### 持久化环境变量（推荐用于生产环境）

**Windows 系统级环境变量：**
```powershell
[System.Environment]::SetEnvironmentVariable('EKP_SQLSERVER_CONN', 'Server=...', 'Machine')
[System.Environment]::SetEnvironmentVariable('CASDOOR_ENDPOINT', 'http://...', 'Machine')
# ... 其他变量
```

**或者使用 .env 文件（需要在脚本中加载）：**
创建 `.env` 文件（不要提交到 Git）：
```ini
EKP_SQLSERVER_CONN=Server=...
CASDOOR_ENDPOINT=http://...
CASDOOR_CLIENT_ID=...
CASDOOR_CLIENT_SECRET=...
CASDOOR_DEFAULT_OWNER=fzswjtOrganization
```

---

## 📅 日常使用

### 1. 增量同步（推荐）

每日定时执行，只同步变更的数据：

```powershell
# 使用预配置的脚本
.\run-sync.ps1
```

**或直接运行可执行文件：**
```powershell
.\bin\Release\net8.0\SyncEkpToCasdoor.exe
```

**日志输出位置：**
- 控制台实时输出
- `logs/sync_YYYYMMDD_HHmmss.log` 文件

---

### 2. 全量同步

在以下情况需要全量同步：
- 首次部署
- 数据不一致需要修复
- Casdoor 数据被清空或重置

```powershell
# 方法1：删除状态文件
Remove-Item sync_state.json -Force
.\bin\Release\net8.0\SyncEkpToCasdoor.exe

# 方法2：设置环境变量（不会删除状态文件）
$env:SYNC_SINCE_UTC = "1970-01-01T00:00:00Z"
.\bin\Release\net8.0\SyncEkpToCasdoor.exe
```

---

### 3. 定时任务配置

**Windows 任务计划程序：**

1. 打开"任务计划程序"
2. 创建基本任务
3. 触发器：每天凌晨 2:00
4. 操作：启动程序
   - 程序：`powershell.exe`
   - 参数：`-ExecutionPolicy Bypass -File "C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\run-sync.ps1"`
5. 条件：可选，电源、网络等
6. 设置：失败时重试 3 次

**或使用 PowerShell 创建任务：**
```powershell
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -File `"C:\Users\ThinkPad\Desktop\VSCOD\SyncEkpToCasdoor\run-sync.ps1`""
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName "EKP-Casdoor-Sync" -Action $action -Trigger $trigger -Principal $principal -Description "每日同步 EKP 用户和组织到 Casdoor"
```

---

## 🔧 故障排查工具

### 1. 检查 EKP 组织视图

查看视图统计信息（总数、去重数、层级分布）：

```powershell
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --check-org-view
```

**示例输出：**
```
vw_org_structure_sync 总记录: 177
vw_org_structure_sync 去重后ID数: 177
层级分布:
  层级 0: 2
  层级 1: 37
  层级 2: 96
  层级 3: 31
  层级 4: 11
```

---

### 2. 查看组织视图示例数据

查看前 20 条组织记录的详细字段：

```powershell
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --peek-org-view
```

**示例输出：**
```
示例数据: id | name | display_name | parent_id | type | owner
  16f1c1a49710197e91a393841508bb01 | ... | 安全质量部 | 16f1c1a4910426f41649fd14862b99a1 | department | fzswjtOrganization
  ...
```

---

### 3. 修复组织视图

如果视图定义过期或损坏，重新创建视图：

```powershell
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --fix-view
```

---

### 4. 更新用户视图（添加密码字段）

为用户视图添加 MD5 密码字段支持：

```powershell
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --update-user-view
```

---

### 5. 检查 Casdoor 数据库

**前置条件：** 设置 Casdoor 数据库连接环境变量

```powershell
$env:CASDOOR_DB_HOST = "172.16.10.110"
$env:CASDOOR_DB_PORT = "3306"
$env:CASDOOR_DB_USER = "root"
$env:CASDOOR_DB_PASSWORD = "zhangjing"
$env:CASDOOR_DB_NAME = "casdoor"

.\bin\Release\net8.0\SyncEkpToCasdoor.exe --check-casdoor-db
```

**示例输出：**
```
连接 Casdoor 数据库 172.16.10.110:3306/casdoor ...
✓ 已连接

`group` 表字段:
  - owner varchar(100)
  - name varchar(100)
  - parent_id varchar(100)
  ...

统计 owner=fzswjtOrganization 的组织:
  总数: 181
  parent_id 为空: 0, 非空: 181

示例(前20条): name | display_name | parent_id
  ...

检查根节点公司:
  福州市城市排水有限公司: ✓ 正确(parent_id=owner)
  危险作业学习系统组织: ✓ 正确(parent_id=owner)
```

---

### 6. 从 CSV 批量修复 parent_id

如果 Casdoor 中的 parent_id 不正确，使用导出的 CSV 批量修复：

```powershell
# 使用最新的 CSV（自动查找 logs/organization_hierarchy_*.csv）
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --fix-casdoor-parentid-from-csv

# 或指定 CSV 文件
$env:CASDOOR_HIERARCHY_CSV = "C:\path\to\hierarchy.csv"
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --fix-casdoor-parentid-from-csv
```

**重要说明：**
- 此工具直接修改 Casdoor 数据库，请谨慎使用
- 修复前会自动备份到事务中，失败会自动回滚
- 根节点（无父级）的 parent_id 会自动设为 owner

---

## ❓ 常见问题

### Q1: 同步后 Casdoor UI 看不到组织树？

**可能原因：** 根节点的 parent_id 未设置为 owner

**解决方案：**
```powershell
# 检查数据库中根节点的 parent_id
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --check-casdoor-db

# 如果根节点 parent_id 不正确，使用 CSV 修复
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --fix-casdoor-parentid-from-csv
```

---

### Q2: 同步报错 "Unknown column 'parent_name'"？

**原因：** Casdoor 数据库版本较旧，group 表没有 parent_name 列

**解决方案：** 
- 程序已自动兼容，会跳过 parent_name 字段
- 重新编译并运行：`dotnet build -c Release`

---

### Q3: 组织层级只显示两层？

**可能原因：**
1. parent_id 设置不正确（都指向顶层公司）
2. 根节点 parent_id 未设为 owner

**排查步骤：**
```powershell
# 1. 检查 Casdoor 数据库中的 parent_id 分布
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --check-casdoor-db

# 2. 检查根节点是否正确
# 输出应显示：✓ 正确(parent_id=owner)

# 3. 如果不正确，执行修复
.\bin\Release\net8.0\SyncEkpToCasdoor.exe --fix-casdoor-parentid-from-csv

# 4. 刷新 Casdoor UI
```

---

### Q4: 用户同步后没有组织归属？

**可能原因：**
1. `vw_user_group_membership` 视图不存在或为空
2. 环境变量 `EKP_USER_GROUP_VIEW` 未设置

**解决方案：**
```powershell
# 检查视图是否存在
# 连接 EKP 数据库，执行：
SELECT COUNT(*) FROM vw_user_group_membership;

# 如果视图不存在，参考 CREATE_MEMBERSHIP_VIEW.sql 创建
# 然后重新全量同步
Remove-Item sync_state.json -Force
.\bin\Release\net8.0\SyncEkpToCasdoor.exe
```

---

### Q5: 密码同步不生效？

**检查清单：**
1. 用户视图是否包含 `password_md5` 字段？
   ```powershell
   .\bin\Release\net8.0\SyncEkpToCasdoor.exe --update-user-view
   ```

2. 环境变量配置是否正确？

3. 重新全量同步：
   ```powershell
   Remove-Item sync_state.json -Force
   .\bin\Release\net8.0\SyncEkpToCasdoor.exe
   ```

---

### Q6: 日志文件编码乱码？

**解决方案：**
- 日志已统一为 UTF-8 编码
- 使用支持 UTF-8 的编辑器打开（VS Code、Notepad++ 等）
- PowerShell 查看：
  ```powershell
  Get-Content logs\sync_*.log -Encoding UTF8 | Select-Object -Last 50
  ```

---

## 📊 监控与维护

### 查看最近的同步日志

```powershell
# 查看最新日志的最后 50 行
Get-Content (Get-ChildItem logs\sync_*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName -Tail 50 -Encoding UTF8
```

### 清理旧日志（保留最近 30 天）

```powershell
Get-ChildItem logs\*.log | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item -Force
```

### 健康检查脚本

创建 `health-check.ps1`：
```powershell
# 检查同步是否正常运行
$latestLog = Get-ChildItem logs\sync_*.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($latestLog.LastWriteTime -lt (Get-Date).AddHours(-25)) {
    Write-Error "同步超过 25 小时未运行！"
    exit 1
}

$content = Get-Content $latestLog.FullName -Tail 20 -Encoding UTF8
if ($content -match "同步失败") {
    Write-Error "最近一次同步失败！"
    exit 1
}

Write-Host "同步状态正常 ✓" -ForegroundColor Green
```

---

## 🔐 安全建议

1. **不要在代码或脚本中硬编码密码**
   - 使用环境变量或密钥管理系统

2. **限制数据库访问权限**
   - EKP 数据库：只读权限即可
   - Casdoor 数据库：仅在必要时使用（故障排查）

3. **定期审计日志**
   - 检查异常的同步行为
   - 监控失败次数

4. **备份同步状态文件**
   ```powershell
   Copy-Item sync_state.json sync_state.json.bak -Force
   ```

---

## 📞 技术支持

- **日志位置：** `logs/sync_YYYYMMDD_HHmmss.log`
- **状态文件：** `sync_state.json`
- **配置文档：** `README.md`
- **变更历史：** `CHANGELOG_MD5_PASSWORD.md`

如有问题，请提供：
1. 完整的错误日志
2. 环境变量配置（脱敏后）
3. 同步前后的 Casdoor 截图

---

**最后更新：** 2025-10-30
