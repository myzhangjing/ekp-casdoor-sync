# EKP 到 Casdoor 字段映射说明

## 概述

本文档详细说明了 EKP 数据库视图字段与 Casdoor API 字段之间的对应关系。

## 组织同步 (vw_org_structure_sync)

### EKP 视图字段

| 字段名 | 类型 | 说明 | 示例值 |
|--------|------|------|--------|
| `Id` | NVARCHAR | 组织唯一标识 | `16f1c1a492fd930d29dd4d2431f925ed` |
| `name` | NVARCHAR | 组织名称(通常与Id相同) | `16f1c1a492fd930d29dd4d2431f925ed` |
| `display_name` | NVARCHAR | 组织显示名称 | `人力资源部` |
| `owner` | NVARCHAR | 组织所有者 | `fzswjtOrganization` |
| `parent_group` | NVARCHAR | 父组织ID | `16f1c1a4910426f41649fd14862b99a1` |
| `type` | NVARCHAR | 组织类型 | `Physical` |
| `created_time` | DATETIME | 创建时间 | `/Date(1576724482337)/` |
| `updated_time` | DATETIME | 更新时间 | `/Date(1581329762583)/` |
| `department` | NVARCHAR | 部门路径 | `福州市城市排水有限公司` |
| `dept_name` | NVARCHAR | 部门名称 | `人力资源部` |
| `company_name` | NVARCHAR | 公司名称 | `福州市城市排水有限公司` |
| `parent_id` | NVARCHAR | 父部门ID | `16f1c1a4910426f41649fd14862b99a1` |
| `company_id` | NVARCHAR | 公司ID | `16f1c1a4910426f41649fd14862b99a1` |
| `is_enabled` | BIT | 是否启用 | `true` |

### Casdoor Group API 字段

| 字段名 | 类型 | 必填 | 说明 | 映射来源 |
|--------|------|------|------|---------|
| `owner` | string | ✓ | 组织拥有者 | `owner` 或默认值 `fzswjtOrganization` |
| `name` | string | ✓ | 组名(唯一标识) | `Id` |
| `displayName` | string | ✓ | 显示名称 | `display_name` |
| `type` | string | ✓ | 组织类型 | `type` (如 `Physical`) |
| `parentId` | string | - | 父组织ID | `parent_id` (如果存在) |
| `isEnabled` | boolean | - | 是否启用 | `is_enabled` |
| `createdTime` | string | - | 创建时间 | `created_time` (转换为ISO8601) |
| `updatedTime` | string | - | 更新时间 | `updated_time` (转换为ISO8601) |

## 用户同步 (vw_casdoor_users_sync)

### EKP 视图字段

| 字段名 | 类型 | 说明 | 示例值 |
|--------|------|------|--------|
| `Id` | NVARCHAR | 用户唯一标识 | `004d942928a0a733538af8807eb687a8` |
| `username` | NVARCHAR | 用户名(手机号) | `13763899453` |
| `display_name` | NVARCHAR | 显示名称 | `张华连` |
| `email` | NVARCHAR | 邮箱地址 | `null` (EKP中通常为空) |
| `phone` | NVARCHAR | 手机号 | `13763899453` |
| `owner` | NVARCHAR | 用户所属组织 | `fzswjtOrganization` |
| `created_at` | DATETIME | 创建时间 | `/Date(1712495097943)/` |
| `updated_at` | DATETIME | 更新时间 | `/Date(1755510741287)/` |
| `password` | NVARCHAR | 密码(MD5哈希) | `eb5ea7f5b326609a4b0f8556b6548e8d` |
| `is_active` | BIT | 是否激活 | `false` |
| `is_deleted` | BIT | 是否已删除 | `false` |
| `affiliation` | NVARCHAR | 部门隶属关系 | `危险作业学习系统组织/健水管网维护` |
| `dept_name` | NVARCHAR | 部门名称 | `健水管网维护` |
| `company_name` | NVARCHAR | 公司名称 | `危险作业学习系统组织` |
| `dept_id` | NVARCHAR | 部门ID | `18e7a3cfdcf31423fdedfa5408cb2587` |
| `company_id` | NVARCHAR | 公司ID | `18e389224b660b4d67413f8466285581` |
| `gender` | NVARCHAR | 性别 | `null` |
| `language` | NVARCHAR | 语言 | `null` |
| `last_pwd_change_time` | DATETIME | 最后修改密码时间 | `null` |
| `work_phone_ext` | NVARCHAR | 工作电话分机 | `null` |
| `is_available_ext` | BIT | 是否可用(扩展) | `false` |
| `is_abandon_ext` | BIT | 是否废弃(扩展) | `false` |
| `groups` | NVARCHAR | 所属组(JSON数组) | `["fzswjtOrganization/18e7a3cf..."]` |
| `type` | NVARCHAR | 用户类型 | `normal-user` |

### Casdoor User API 字段

| 字段名 | 类型 | 必填 | 说明 | 映射来源 |
|--------|------|------|------|---------|
| `owner` | string | ✓ | 用户所属组织 | `owner` 或默认值 `fzswjtOrganization` |
| `name` | string | ✓ | 用户名(唯一标识) | `username` (经过slug处理) |
| `displayName` | string | ✓ | 显示名称 | `display_name` |
| `id` | string | - | 外部系统ID | `Id` |
| `email` | string | - | 邮箱地址 | `email` (如果非空) |
| `phone` | string | - | 手机号 | `phone` (如果非空) |
| `password` | string | - | 密码 | `password` (MD5哈希) |
| `gender` | string | - | 性别 | `gender` (如果非空) |
| `language` | string | - | 语言 | `language` (如果非空) |
| `affiliation` | string | - | 部门隶属关系 | `affiliation` (如果非空) |
| `type` | string | - | 用户类型 | `type` 或默认值 `normal-user` |
| `groups` | array | - | 所属组列表 | `groups` (解析JSON数组) |
| `createdTime` | string | - | 创建时间 | `created_at` (转换为ISO8601) |
| `updatedTime` | string | - | 更新时间 | `updated_at` (转换为ISO8601) |

## 关键映射规则

### 1. Owner (组织拥有者)

- **默认值**: `fzswjtOrganization`
- **优先级**: 
  1. EKP 视图中的 `owner` 字段
  2. 如果 `owner` 为空,使用 `company_name` 字段
  3. 都为空时使用默认值

### 2. Name (用户名/组名)

- **组织名**: 直接使用 `Id` 字段
- **用户名**: 使用 `username` 字段,经过 Slug 处理(小写、去特殊字符)
  - 示例: `13763899453` → `13763899453`
  - 特殊字符转换为 `-`

### 3. ParentId (父组织ID)

- 优先使用 `dept_id` 字段
- 如果 `dept_id` 为空,使用 `parent_id` 字段
- **注意**: 父组织必须在子组织之前创建,否则会导致关联失败

### 4. Groups (用户所属组)

- EKP 中是 JSON 字符串数组格式: `["fzswjtOrganization/18e7a3cf..."]`
- 需要反序列化后传递给 Casdoor API
- 格式: `{owner}/{group_id}`

### 5. Type (类型)

- **组织类型**: `Physical` (物理组织)
- **用户类型**: `normal-user` (普通用户)

## 同步顺序

为确保数据完整性,同步必须按以下顺序进行:

1. **组织同步** - 按层级深度从浅到深排序,确保父组织先于子组织创建
2. **用户同步** - 创建所有用户记录
3. **成员关系同步** - 建立用户与组织的关联关系

## 常见问题

### Q1: 为什么有些用户同步失败?

**A**: 检查以下几点:
- 用户所属的组织是否已在 Casdoor 中创建
- 用户的 `groups` 字段中引用的组织ID是否存在
- `owner` 字段值是否正确

### Q2: ParentId 关联失败怎么办?

**A**: 
- 确保父组织已经创建(检查同步日志中的顺序)
- 检查 `parent_id` 字段值是否与实际父组织的 `Id` 匹配
- 如果父组织不存在,系统会自动将该字段置为 `null`

### Q3: 如何处理空字段?

**A**: 
- 简化版仓储只提交非空字段
- Casdoor API 会使用默认值填充缺失字段
- 布尔类型字段默认为 `false`

## 调试建议

1. 查看同步日志,确认 HTTP 请求和响应
2. 检查 Casdoor 后台管理界面,确认数据是否正确创建
3. 使用 `--dry-run` 参数测试映射逻辑而不实际写入
4. 查询 EKP 视图数据,确认源数据质量:
   ```sql
   SELECT TOP 10 * FROM vw_casdoor_users_sync WHERE updated_at > '2024-01-01'
   SELECT TOP 10 * FROM vw_org_structure_sync WHERE updated_time > '2024-01-01'
   ```

## 参考资料

- [Casdoor API 文档](https://door.casdoor.com/swagger/)
- [Casdoor User Model](http://sso.fzcsps.com/swagger/swagger.json#/definitions/object.User)
- [Casdoor Group Model](http://sso.fzcsps.com/swagger/swagger.json#/definitions/object.Group)
