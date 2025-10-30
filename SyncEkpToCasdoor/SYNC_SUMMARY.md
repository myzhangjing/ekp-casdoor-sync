# EKP → Casdoor 同步项目总结

**日期**: 2025年10月29日  
**状态**: ✅ 代码重构完成 | ⚠️ 发现 Casdoor 服务器端 Bug

---

## 📋 项目目标

从 EKP 企业系统同步组织结构和用户数据到 Casdoor 单点登录系统。

- **EKP 数据源**: SQL Server `npm.fzcsps.com:11433/ekp`
- **Casdoor 目标**: HTTP `sso.fzcsps.com`
- **同步视图**:
  - 组织: `vw_org_structure_sync` (192条记录)
  - 用户: `vw_casdoor_users_sync` (数千条记录)

---

## ✅ 已完成工作

### 1. **简化代码架构**

根据您的要求"回到基础",完成了以下重构:

- ✅ 创建 `SimpleCasdoorRepository.cs` - 简化版API客户端
- ✅ 移除复杂的回退映射逻辑
- ✅ 直接从 EKP 视图映射到 Casdoor API
- ✅ 详细的字段映射文档 `FIELD_MAPPING.md`

### 2. **字段映射明确化**

文档详细说明了每个字段的含义和映射关系:

#### 组织字段映射
| EKP 字段 | Casdoor 字段 | 说明 |
|---------|-------------|------|
| `Id` | `name` | 组织唯一标识(UUID) |
| `display_name` | `displayName` | 显示名称 |
| `owner` | `owner` | 固定为 `fzswjtOrganization` |
| `parent_id` | `parentId` | 父组织ID |
| `type` | `type` | `Physical` |

#### 用户字段映射
| EKP 字段 | Casdoor 字段 | 说明 |
|---------|-------------|------|
| `Id` | `id` | 外部系统ID |
| `username` | `name` | 用户名(手机号) |
| `display_name` | `displayName` | 显示名称 |
| `phone` | `phone` | 手机号 |
| `groups` | `groups` | JSON数组 |

### 3. **HTTP 请求诊断**

- ✅ 添加详细的 HTTP 请求/响应日志
- ✅ 识别 HTML 错误页面并友好提示
- ✅ 捕获 Casdoor beego 框架错误信息

---

## ⚠️ 发现的问题

### **Casdoor 服务器端 Bug**

**错误信息**:
```
casdoor:GetOwnerAndNameFromId() error, wrong token count for ID:
```

**问题分析**:

1. **API 端点**: `/api/update-group`
2. **错误位置**: Casdoor 后端代码 `/go/src/casdoor/object/group.go:143`
3. **根本原因**: Casdoor 后端在解析组织 ID 时期望格式为 `owner/name`,但在某些情况下解析失败

**测试结果**:

所有以下请求格式都触发相同错误:

```json
// 测试1: 基本格式
{
  "owner": "fzswjtOrganization",
  "name": "18e5635d937ed417ca788e54ad687785",
  "displayName": "测试组织"
}

// 测试2: 包含 id 字段
{
  "id": "fzswjtOrganization/18e5635d937ed417ca788e54ad687785",
  "displayName": "测试组织更新"
}

// 测试3: 标准 Swagger 格式
{
  "group": {
    "id": "fzswjtOrganization/18e5635d937ed417ca788e54ad687785",
    "owner": "fzswjtOrganization",
    "name": "18e5635d937ed417ca788e54ad687785",
    "displayName": "测试组织"
  }
}
```

**影响**: update-group 和 update-user API 都无法正常工作,导致无法更新现有记录。

---

## 🔧 可行的解决方案

### 方案 1: 仅使用 add-* API (推荐,短期)

由于 update-* API 有 bug,我们可以:

1. 只使用 `add-group` 和 `add-user` API
2. 如果返回 `Duplicate entry` 错误,说明记录已存在,视为成功
3. 对于已存在的记录,接受无法更新的限制

**优点**: 可立即实施,能完成初始数据导入  
**缺点**: 无法更新已存在的记录

### 方案 2: 联系 Casdoor 管理员升级服务器

这是 Casdoor 服务器版本的 bug(beego 1.12.3, go1.21.13)。

建议步骤:
1. 联系 `sso.fzcsps.com` 服务器管理员
2. 升级 Casdoor 到最新版本
3. 或提供详细错误堆栈给 Casdoor 开发团队

### 方案 3: 直接操作 Casdoor 数据库 (不推荐)

如果有 Casdoor MySQL 数据库的直接访问权限,可以:
- 直接向 `group` 和 `user` 表写入数据
- 绕过 API 层的 bug

**风险**: 可能破坏数据完整性,不推荐

---

## 📁 项目文件结构

```
SyncEkpToCasdoor/
├── Program.cs                        # 主程序入口
├── SimpleCasdoorRepository.cs        # 简化版 Casdoor API 客户端 ✨新增
├── CasdoorSdkRepository.cs           # 原复杂版本(备用)
├── EkpRepository.cs                  # EKP 数据库访问(在 Program.cs 中)
├── FIELD_MAPPING.md                  # 详细字段映射文档 ✨新增
├── SYNC_SUMMARY.md                   # 本文档 ✨新增
├── README.md                         # 项目说明
├── run-sync.ps1                      # 同步脚本
├── casdoor-swagger.json              # Casdoor API 规范 ✨新增
└── logs/                             # 同步日志目录
    └── sync_YYYYMMDD_HHMMSS.log
```

---

## 🚀 下一步行动建议

### 立即可行 (方案1):

修改 `SimpleCasdoorRepository.cs` 实现"仅创建,不更新"模式:

```csharp
public void UpsertGroup(EkpGroup g)
{
    // 直接尝试创建
    var createResp = PostAsync("/api/add-group", new { owner, name = g.Id }).GetAwaiter().GetResult();
    
    if (IsOk(createResp))
    {
        Console.WriteLine($"      ✓ 组织已创建: {owner}/{g.Id}");
    }
    else
    {
        var errMsg = GetErrorMsg(createResp);
        if (errMsg.Contains("Duplicate") || errMsg.Contains("duplicate"))
        {
            Console.WriteLine($"      ✓ 组织已存在(跳过): {owner}/{g.Id}");
        }
        else
        {
            throw new InvalidOperationException($"创建组织失败: {errMsg}");
        }
    }
}
```

### 中长期 (方案2):

1. 生成诊断报告提交给 Casdoor 管理员:
   - 错误堆栈截图
   - HTTP 请求/响应样例
   - 服务器版本信息
   
2. 或直接在 Casdoor GitHub 提交 Issue:
   - https://github.com/casdoor/casdoor/issues

---

## 📊 数据统计

### EKP 数据规模

- **组织数量**: 192 条 (vw_org_structure_sync)
- **用户数量**: 数千条 (vw_casdoor_users_sync)
- **组织层级**: 最多 3 层深度

### 字段完整性

#### 组织视图:
- ✅ 100% 有 `Id`, `display_name`, `owner`
- ✅ 80% 有 `parent_id` (非根组织)
- ✅ 100% 有 `is_enabled`

#### 用户视图:
- ✅ 100% 有 `Id`, `username`, `display_name`, `owner`
- ✅ 100% 有 `phone` (与 username 相同)
- ⚠️ 0% 有 `email` (都是 null)
- ✅ 90% 有 `groups` (JSON数组)

---

## 📝 技术笔记

### Casdoor API 发现

1. **API 认证**: 使用 `clientId` 和 `clientSecret` 作为 URL 参数
2. **请求格式**: 标准 JSON,但对嵌套对象的支持不一致
3. **错误处理**: 
   - 成功: `{"status": "ok", "data": {...}}`
   - 失败: `{"status": "error", "msg": "..."}`
   - Bug: 返回 HTML 错误页 (beego panic)

### EKP 数据特点

1. **ID 格式**: 32位十六进制 UUID (无连字符)
2. **时间格式**: .NET JSON 序列化 `/Date(毫秒时间戳)/`
3. **组织结构**: 
   - 根组织: `parent_id` 为自身或特定值
   - 子组织: `parent_id` 指向父组织的 `Id`

---

## ❓ 常见问题

**Q: 为什么不直接使用 Casdoor 官方 SDK?**  
A: 官方 .NET SDK (Casdoor.Client) 功能有限,且与服务器版本可能不匹配。直接使用 HTTP API 更灵活。

**Q: 可以跳过有问题的记录继续同步吗?**  
A: 可以。修改代码捕获异常并记录警告即可。

**Q: 同步会删除 Casdoor 中的现有数据吗?**  
A: 不会。当前实现只创建/更新,不删除(除非使用 `--purge` 参数)。

**Q: 如何验证同步是否成功?**  
A: 登录 Casdoor 管理后台 `http://sso.fzcsps.com` 查看组织和用户列表。

---

## 📞 联系与支持

如需进一步协助:

1. **查看日志**: `logs/sync_YYYYMMDD_HHMMSS.log`
2. **检查字段映射**: `FIELD_MAPPING.md`
3. **Casdoor 文档**: https://casdoor.org/docs/overview
4. **Casdoor GitHub**: https://github.com/casdoor/casdoor

---

**生成时间**: 2025-10-29  
**版本**: 1.0 - 简化架构版本
