# EKP Groups字段深度分析报告

## 执行时间
2025-10-29

## 分析目的
你提出"EKP提供的groups字段我觉得不靠谱",需要深度分析以确定是否可靠以及如何正确使用。

---

## 关键发现

### ❌ 问题1: Groups字段格式为JSON字符串,不是纯文本

**实际格式:**
```
["fzswjtOrganization/18e563710626c653c4e74bc40a38c116"]
```

**问题:**
- 视图直接生成JSON数组格式字符串,而不是简单的分号分隔
- 需要JSON解析才能提取组织ID
- 之前的代码按分号分隔处理,完全无法解析此格式

**视图SQL定义:**
```sql
CAST(
    N'["fzswjtOrganization/' + ISNULL(pd.DeptId, '') + N'"]'
    AS nvarchar(4000)
) AS groups
```

### ⚠️ 问题2: 每个用户只有一个组织

**统计数据:**
- 总用户数: 1787
- Groups为NULL: 0
- Groups为空串: 0
- 有Groups数据: 1787 (100%)
- **所有Groups长度均为55字符** (固定格式)
- **分号数量: 0** (说明没有多个组织)

**影响:**
- 用户可能实际属于多个部门/组织,但视图只取了`pd.DeptId`(单个部门)
- 不支持用户的多组织归属场景
- 数据模型过于简化

### ❌ 问题3: 组织ID完全不匹配 - 数据一致性问题

**测试结果:**
- 抽样检查20个用户的Groups中的组织ID
- **匹配数: 0**
- **不匹配数: 20** (100%不匹配率!)

**示例:**
```
用户: 13763899453
Groups: ["fzswjtOrganization/18e7a3cfdcf31423fdedfa5408cb2587"]
组织ID: 18e7a3cfdcf31423fdedfa5408cb2587
在vw_org_structure_sync中: ✗ 不存在
```

**可能原因:**
1. `pd.DeptId` 使用的是不同的ID体系(可能是EKP内部ID)
2. `vw_org_structure_sync`导出的组织ID与用户表关联的ID不一致
3. 数据源或视图逻辑有误

---

## 影响分析

### 对Casdoor同步的影响

**当前流程:**
1. ✅ LoadCasdoorGroupMapping() - 成功加载192个组织
2. ❌ Groups解析 - 提取ID为 `18e7a3cfdcf31423...`
3. ❌ 映射查找 - 在`_casdoorGroupMapping`中找不到该ID
4. ❌ 结果: `casdoorGroups = null` (未找到任何匹配的组织)
5. ⚠️  用户创建时groups为空数组`[]`

**实际效果:**
- 即使代码正确解析了JSON格式
- 由于ID不匹配,所有用户都无法关联到组织
- **Groups字段事实上不可用!**

---

## 已实施的修复

### 代码修改: SimpleCasdoorRepository.cs

**修改前:** 按分号分隔解析
```csharp
var ekpGroupIds = u.Groups.Split(';', StringSplitOptions.RemoveEmptyEntries)
    .Select(g => g.Trim())
    ...
```

**修改后:** JSON格式解析
```csharp
if (u.Groups.TrimStart().StartsWith("["))
{
    // JSON数组格式
    var jsonGroups = System.Text.Json.JsonSerializer.Deserialize<string[]>(u.Groups);
    if (jsonGroups != null)
    {
        ekpGroupIds.AddRange(jsonGroups
            .Select(g => {
                // 提取组织ID: "fzswjtOrganization/18e563..." -> "18e563..."
                var parts = g.Trim().Split('/');
                return parts.Length > 1 ? parts[parts.Length - 1] : g.Trim();
            }));
    }
}
else
{
    // 兼容分号分隔格式
    ...
}
```

**增加警告日志:**
```csharp
if (ekpGroupIds.Count > 0 && mappedGroups.Count == 0)
{
    Console.WriteLine($"      警告: EKP Groups中的组织ID ({string.Join(", ", ekpGroupIds.Take(3))}) 在Casdoor组织映射中未找到");
}
```

---

## 根本解决方案

### 方案1: 修复EKP视图(推荐)

**需要DBA或EKP管理员:**

修改`vw_casdoor_users_sync`视图的Groups字段定义,使用**与vw_org_structure_sync一致的ID**:

```sql
-- 当前(错误):
CAST(
    N'["fzswjtOrganization/' + ISNULL(pd.DeptId, '') + N'"]'
    AS nvarchar(4000)
) AS groups

-- 修复后(需要确认正确的ID字段):
-- 假设vw_org_structure_sync使用的是org.id字段
CAST(
    N'["fzswjtOrganization/' + ISNULL(org.id, '') + N'"]'
    AS nvarchar(4000)
) AS groups

-- 或者支持多组织(如果有关联表):
CAST(
    '[' + STRING_AGG(
        '"fzswjtOrganization/' + org.id + '"', 
        ','
    ) + ']'
    AS nvarchar(4000)
) AS groups
```

**优点:**
- 一次修复,所有同步自动受益
- 数据一致性有保障
- 支持多组织归属

### 方案2: 绕过Groups字段,直接查询关联表

**在代码中实现:**

不使用视图的Groups字段,而是:
1. 从`sys_org_person`表获取用户的`fd_parentorgid`和`fd_parentid`
2. 递归向上查询部门层级
3. 收集所有相关组织ID
4. 构建完整的groups数组

**优点:**
- 不依赖视图
- 可以自定义组织归属逻辑
- 更灵活

**缺点:**
- 需要额外的数据库查询
- 性能可能较差(1787用户 × 递归查询)
- 维护成本高

### 方案3: 使用Membership视图(如果存在)

检查是否有专门的成员关系视图,例如:
```sql
SELECT user_id, group_id, owner, group_name 
FROM vw_user_group_membership  -- 假设的视图名
```

如果有,直接使用该视图构建用户-组织映射,而不依赖Groups字段。

---

## Casdoor API的Groups字段要求

### 正确格式

**Casdoor add-user API期望的groups参数:**
```json
{
  "user": {
    "owner": "fzswjtOrganization",
    "name": "13763899453",
    "groups": [
      "fzswjtOrganization/16f1c1a492fd930d29dd4d2431f925ed",
      "fzswjtOrganization/1702ea5397ada9df93bf9534b438ebf9"
    ]
  }
}
```

**关键点:**
- `groups`是字符串数组
- 每个元素格式: `"{owner}/{groupName}"`
- `groupName`必须是Casdoor中实际存在的组织的`name`字段值
- **必须与Casdoor组织表完全一致**

### 验证方法

测试groups字段是否正确:
```bash
# 1. 获取所有组织
curl "http://sso.fzcsps.com/api/get-groups?owner=fzswjtOrganization&clientId=xxx&clientSecret=xxx"

# 2. 记录返回的组织name字段
# 3. 在创建用户时使用这些name构建groups数组
```

---

## 测试建议

### 手动测试流程

1. **获取一个真实的组织ID:**
```sql
SELECT TOP 1 id, name, display_name 
FROM vw_org_structure_sync 
WHERE owner = 'fzswjtOrganization'
```

2. **手动创建测试用户:**
```bash
curl -X POST "http://sso.fzcsps.com/api/add-user?clientId=aecd00a352e5c560ffe6&clientSecret=4402518b20dd191b8b48d6240bc786a4f847899a" \
  -H "Content-Type: application/json" \
  -d '{
    "user": {
      "owner": "fzswjtOrganization",
      "name": "test_groups_user",
      "displayName": "测试用户",
      "groups": ["fzswjtOrganization/16f1c1a492fd930d29dd4d2431f925ed"]
    }
  }'
```

3. **验证:**
```bash
curl "http://sso.fzcsps.com/api/get-user?id=fzswjtOrganization/test_groups_user&clientId=xxx&clientSecret=xxx"
# 检查返回的groups字段
```

---

## 下一步行动

### 立即可做

1. ✅ **代码已修复** - 支持JSON格式解析
2. ✅ **添加警告日志** - 显示ID不匹配问题
3. ⏳ **运行一次同步** - 观察警告日志,确认问题

### 需要你决定

**选择解决方案:**

**A. 修复EKP视图** (推荐,一劳永逸)
- 需要: 联系DBA或EKP管理员
- 修改: `vw_casdoor_users_sync`的Groups字段定义
- 确保: 使用与`vw_org_structure_sync`一致的ID

**B. 绕过Groups字段** (临时方案)
- 我可以: 修改代码直接查询EKP的组织关联表
- 缺点: 性能较差,维护成本高

**C. 暂时不同步groups** (快速方案)
- 先同步: 用户和组织基本信息
- 后续: 通过Casdoor UI或其他方式手动配置成员关系

### 你需要回答的问题

1. **vw_org_structure_sync中的id字段是什么?**
   - 是`fd_id`还是其他字段?
   - 与`pd.DeptId`是同一个体系吗?

2. **EKP是否有用户-组织关联表?**
   - 表名是什么?
   - 字段结构如何?

3. **你倾向哪个解决方案?**
   - A. 修复视图
   - B. 代码绕过
   - C. 暂不同步groups

---

## 总结

**你的直觉是对的** - EKP提供的Groups字段确实不可靠!

**三大问题:**
1. ❌ 格式错误 (JSON字符串未声明)
2. ❌ 数据不完整 (每用户只有1个组织)
3. ❌ ID不匹配 (100%的ID在组织表中不存在)

**当前状态:**
- 代码已修复,可以解析JSON格式
- 但由于ID不匹配,实际上所有用户的groups仍然为空
- 需要修复数据源才能真正解决问题

**建议行动:**
修复EKP视图的Groups字段定义,使用正确的组织ID体系。
