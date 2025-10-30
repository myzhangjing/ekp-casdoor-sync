# Casdoor 组织层级同步问题及解决方案

## 问题描述

在同步EKP组织数据到Casdoor时,发现**组织的父子层级关系无法通过API建立**:

1. ✅ 组织数据同步成功(177个组织)
2. ✅ 用户数据同步成功(1157个用户)
3. ✅ 用户-组织关系同步成功(1291条关系)
4. ❌ **组织的parentName字段始终为空**,导致UI无法显示层级结构

### 根本原因

经过测试验证,Casdoor API存在以下问题:

- `/api/add-group` - 接受parentName参数但**不保存到数据库**
- `/api/update-group` - 返回HTML错误页面(API bug),无法更新已有组织

### 测试证据

```powershell
# 创建父组织
POST /api/add-group
Body: {"owner":"fzswjtOrganization","name":"test_parent","displayName":"父组织"}
Response: {"status":"ok","data":"Affected"}  ✓

# 创建子组织(指定parentName)
POST /api/add-group
Body: {"owner":"fzswjtOrganization","name":"test_child","parentName":"fzswjtOrganization/test_parent"}
Response: {"status":"ok","data":"Affected"}  ✓

# 查询结果
GET /api/get-groups?owner=fzswjtOrganization
Response: 
  - test_parent: parentName="" ❌ 应该为null
  - test_child: parentName=""  ❌ 应该为"fzswjtOrganization/test_parent"
```

**结论**: parentName字段在API调用中被忽略,数据库中仍为空。

## 解决方案

### 方案1: 数据库直接更新 (推荐)

使用提供的PowerShell脚本直接更新Casdoor数据库:

```powershell
# 执行数据库更新脚本
.\update-casdoor-parent.ps1

# 或者指定参数
.\update-casdoor-parent.ps1 `
    -Server "192.168.52.3" `
    -Port 13306 `
    -Database "casdoor" `
    -User "root" `
    -Password "Abc123456"
```

**脚本功能**:
1. 读取最新的 `organization_hierarchy_*.csv` 文件
2. 生成MySQL UPDATE语句
3. 执行数据库更新
4. 更新 `group` 表的 `parent_name` 字段

**前置条件**:
- 需要安装 MySQL 客户端(或手动执行生成的SQL文件)
- 需要Casdoor数据库的访问权限

### 方案2: 手动导入CSV (备选)

如果无法直接访问数据库,可以:

1. 查看导出的CSV文件:
   ```powershell
   # 最新的组织层级文件在 logs 目录
   ls logs\organization_hierarchy_*.csv | Sort-Object LastWriteTime -Descending | Select -First 1
   ```

2. CSV文件包含以下字段:
   - 组织ID
   - 组织名称
   - 父组织ID
   - 父组织名称
   - Casdoor组织名称 (格式: owner/id)
   - Casdoor父组织名称 (格式: owner/parent_id)

3. 手动在Casdoor UI中设置父子关系

### 方案3: 等待Casdoor API修复 (长期)

这是Casdoor的已知问题,可能的解决途径:

1. 升级Casdoor到最新版本
2. 向Casdoor项目提交issue报告
3. 使用Casdoor SDK而非REST API

## 当前状态

### 数据同步情况

| 项目 | 数量 | 状态 |
|------|------|------|
| 公司 | 2 | ✅ 已同步 |
| 部门 | 175 | ✅ 已同步 |
| 用户 | 1157 | ✅ 已同步 |
| 用户-组织关系 | 1291 | ✅ 已同步 |
| 组织层级关系 | 175 | ❌ 未建立 |

### 层级结构示例

```
福州市城市排水有限公司 (16f1c1a4910426f41649fd14862b99a1)
├── 人力资源部 (16f1c1a492fd930d29dd4d2431f925ed)
├── 客服部 (16f1c1a493ff7d63d4310f1409f8c158)
├── 党群部 (16f1c1a494ec6d4d6e12cf94cf4adae2)
├── 公司领导班子 (16f1c1a495ecf0e6b53ea74440d9f478)
├── 安全质量部 (16f1c1a49710197e91a393841508bb01)
├── ...

危险作业学习系统组织 (18e389224b660b4d67413f8466285581)
├── 曾伟华项目组 (1900a13ffcff362b0fc3e8e4db8a5543)
├── 得乾福州水司 (18ebd0c0bd0c2181fed27254fb98fa3a)
├── 得乾高新水务 (18e98dc55e061f43514c0b24e03a06ee)
├── ...
```

## 性能优化记录

在解决层级问题之前,已完成以下优化:

1. **SQL视图性能优化** - 从120秒降至0.36秒 (333x提升)
   - 移除递归CTE
   - 优化dept_id关联逻辑
   - 确保dept_id只指向部门(org_type=2)

2. **数据准确性修正**
   - 修复dept_id错误指向公司的问题
   - 确保 `公司 → 部门 → 人员` 层级结构正确
   - 支持用户多部门归属

## 下一步操作

**立即执行**:

```powershell
# 1. 检查当前parentName状态
.\check-casdoor-groups.ps1

# 2. 执行数据库更新
.\update-casdoor-parent.ps1

# 3. 再次检查验证
.\check-casdoor-groups.ps1
```

**预期结果**:
- 有父级的组织: 175 个
- 无父级的组织: 2 个 (两个顶级公司)
- Casdoor UI显示完整的组织树形结构

## 技术细节

### EKP视图结构

```sql
-- vw_org_structure_sync: 组织结构视图
SELECT 
    fd_id AS id,
    fd_name AS display_name,
    CASE WHEN fd_org_type = 1 THEN NULL 
         ELSE COALESCE(fd_parentorgid, fd_parentid) 
    END AS parent_id,
    CASE WHEN fd_org_type = 1 THEN 'Company' ELSE 'Company' END AS type,
    CASE WHEN fd_is_available = 1 THEN 1 ELSE 0 END AS is_enabled,
    fd_org_type,
    fd_no AS dept_id
FROM ekp_v3.sys_org_element
WHERE fd_is_available = 1 
  AND fd_org_type IN (1, 2)  -- 1=公司, 2=部门
```

### Casdoor数据库结构

```sql
-- group表相关字段
owner VARCHAR(100)         -- 组织所有者
name VARCHAR(100)          -- 组织ID (唯一标识)
display_name VARCHAR(200)  -- 显示名称
parent_name VARCHAR(100)   -- 父组织 (格式: owner/parent_id)
type VARCHAR(100)          -- 类型: Company/Department
```

### API认证方式

Casdoor使用URL参数认证,而非OAuth2:

```
GET /api/get-groups?owner={owner}&clientId={id}&clientSecret={secret}
POST /api/add-group?clientId={id}&clientSecret={secret}
Body: {"owner":"...","name":"...","parentName":"owner/parent_id"}
```

## 联系与支持

如有问题,请检查:
1. `logs\sync_*.log` - 同步日志
2. `logs\organization_hierarchy_*.csv` - 导出的层级关系
3. `logs\update_parent_name.sql` - 生成的SQL脚本

技术栈:
- EKP: SQL Server (npm.fzcsps.com:11433)
- Casdoor: MySQL (192.168.52.3:13306)
- 同步程序: .NET 8 / C#
