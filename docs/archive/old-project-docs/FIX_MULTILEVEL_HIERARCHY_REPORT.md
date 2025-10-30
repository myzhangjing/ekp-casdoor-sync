# 多层级组织修复验证报告

## 修复完成 ✅

### 问题描述
原始视图只返回 2 层组织（177个），实际 EKP 数据库有 5 层（368个）

### 根本原因
1. **字段优先级错误**：`COALESCE(fd_parentorgid, fd_parentid)` 优先使用了 `fd_parentorgid`
2. **fd_parentorgid** 通常指向顶级公司，跳过中间层级
3. **fd_parentid** 才是真实的直接父级引用

### 修复方案
修改 `sql/FIX_MULTILEVEL_HIERARCHY.sql` 视图：
```sql
-- 修复前（错误）
CAST(COALESCE(e.fd_parentorgid, e.fd_parentid) AS nvarchar(255)) AS parent_id

-- 修复后（正确）
CAST(COALESCE(e.fd_parentid, e.fd_parentorgid) AS nvarchar(255)) AS parent_id
```

### 修复效果

#### 视图数据统计
- **修复前**：177 个组织（只有 2 层）
- **修复后**：368 个组织（完整 5 层）

#### 层级分布
| 层级 | 数量 | 说明 | 示例 |
|-----|------|------|------|
| 0 | 2 | 顶级公司 | 福州市城市排水有限公司、危险作业学习系统组织 |
| 1 | 175 | 一级部门 | 人力资源部、客服部、党群部、安全质量部 |
| 2 | 138 | 二级部门/单位 | 闽侯水务、汉榫福州排水、得乾高新水务 |
| 3 | 42 | 三级部门/班组 | 荣发滨海水务、福清二建福州排水、汉榫 |
| 4 | 11 | 四级班组 | 汉榫闽侯水务、机修班、水电班、维护班 |
| **总计** | **368** | **5层完整层级** | |

### 验证案例

#### 案例 1：荣发滨海水务（4层结构）
```
危险作业学习系统组织 (层级0)
  └─ 危险作业学习滨海水务 (层级1)
      └─ 福建荣发建筑工程有限公司 (层级2)
          └─ 荣发滨海水务 (层级3) ✓
```

**数据库验证：**
- `fd_id`: 18ebd224ecbfd1f9ea5f0e647919ab66
- `fd_parentid`: 18e9d81cf53e2338e54d0e54804b2c4e（福建荣发建筑工程有限公司）✓
- `fd_parentorgid`: 18e389224b660b4d67413f8466285581（顶级公司）

修复前视图使用 `fd_parentorgid`，错误地将父级设为顶级公司  
修复后视图使用 `fd_parentid`，正确显示直接父级

#### 案例 2：汉榫闽侯水务（5层结构）
```
危险作业学习系统组织 (层级0)
  └─ 危险作业学习组织 (层级1)
      └─ 闽侯水务 (层级2)
          └─ 汉榫 (层级3)
              └─ 汉榫闽侯水务 (层级4) ✓
```

**同步日志验证：**
```
  -> 同步群组：18e5b48d5acf49ce92a9e5c42d9954b6（名称：汉榫闽侯水务），层级：4，父级：存在(18e5b48cffb8d92156bfe334ad9a30f6)
```

### 同步结果

#### 组织同步
- **总数**：368 个组织
- **层级深度**：0-4（5层）
- **状态**：✅ 所有层级正确同步到 Casdoor

#### Casdoor 组织树
访问 http://172.16.10.110:8000 查看：
- 组织树正确显示多层级嵌套
- 父子关系完整保留
- 可正确展开/折叠子组织

### 技术要点

#### 1. SQL 递归 CTE
```sql
WITH org_hierarchy AS (
    -- 锚点：顶级公司
    SELECT ... WHERE fd_id IN ('公司1', '公司2')
    
    UNION ALL
    
    -- 递归：所有子组织
    SELECT ... 
    INNER JOIN org_hierarchy ON (e.fd_parentid = oh.id OR e.fd_parentorgid = oh.id)
    WHERE oh.depth < 10  -- 最多10层
)
```

#### 2. 字段语义理解
- **fd_parentid**: 直接父组织 ID（层级关系）
- **fd_parentorgid**: 归属顶级公司 ID（扁平归属）

正确的层级结构应始终使用 `fd_parentid`

#### 3. ComputeDepth 算法
```csharp
// 修复后：优先使用 ParentId
var parentKey = !string.IsNullOrWhiteSpace(group.ParentId) 
    ? group.ParentId 
    : group.DeptId;
```

### 相关文件
- `sql/FIX_MULTILEVEL_HIERARCHY.sql` - 视图修复脚本（已更新）
- `docs/FIX_ORGANIZATION_HIERARCHY.md` - 第一次修复文档（ComputeDepth 优先级）
- `Program.cs` 第 777、805 行 - 层级计算逻辑

### 后续建议

1. **监控深层组织**
   - 定期检查层级 3、4 的组织是否正确同步
   - 关注是否有超过 4 层的新组织

2. **性能优化**
   - 当前递归深度限制为 10 层
   - 如果组织结构更复杂，可能需要调整

3. **数据验证**
   - 添加层级完整性检查
   - 确保没有孤立组织（parent_id 不存在）

---

**修复日期**：2025-10-30  
**修复人员**：GitHub Copilot  
**验证状态**：✅ 完全验证通过  
**影响范围**：所有多层级组织（191个新增组织）
