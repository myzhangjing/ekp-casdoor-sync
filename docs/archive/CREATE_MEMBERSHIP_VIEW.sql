-- =====================================================
-- 创建用户-组织成员关系视图
-- 用于 SyncEkpToCasdoor 同步工具
-- =====================================================
-- 
-- 此视图用于替代 vw_casdoor_users_sync 中不可靠的 Groups 字段
-- 它为同步工具提供一个清晰、准确的用户与组织的多对多关系映射
--
-- 重要要求:
-- 1. user_id 必须与 vw_casdoor_users_sync.id 一致
-- 2. group_id 必须与 vw_org_structure_sync.id 一致
-- 3. 一个用户可以属于多个组织(多行记录)
-- 4. 如果用户没有组织关系,不应该在此视图中出现
--
-- 示例数据结构:
-- | user_id              | group_id             |
-- |----------------------|----------------------|
-- | 18e5637106267c10f7f8 | 18e5637106267c10f7f8 |  -- 用户1属于组织1
-- | 18e5637106267c10f7f8 | 18e5637106267c11aaaa |  -- 用户1也属于组织2
-- | 19a1234567890abcdef0 | 18e5637106267c11aaaa |  -- 用户2属于组织2
-- =====================================================

-- 方案 A: 基于 EKP 组织成员表 (推荐)
-- 如果您的 EKP 系统中有专门的组织-成员关联表,使用此方案
CREATE OR ALTER VIEW vw_user_group_membership AS
SELECT 
    u.fd_id AS user_id,           -- 用户ID
    o.fd_id AS group_id           -- 组织ID
FROM 
    ekp_org_person u              -- 用户表
    INNER JOIN ekp_org_element e ON e.fd_person_id = u.fd_id  -- 成员关系表
    INNER JOIN ekp_org_org o ON o.fd_id = e.fd_org_id         -- 组织表
WHERE 
    u.fd_is_business = 1          -- 仅业务用户
    AND u.fd_is_available = 1     -- 仅启用用户
    AND o.fd_is_available = 1     -- 仅启用组织
    AND e.fd_is_default = 1;      -- 仅主组织关系 (如果支持多组织,去掉此条件)

GO

-- 方案 B: 基于用户的部门字段 (简化方案)
-- 如果没有专门的关联表,可以使用用户的部门ID作为组织ID
-- 注意: 此方案每个用户只能关联一个组织
/*
CREATE OR ALTER VIEW vw_user_group_membership AS
SELECT 
    fd_id AS user_id,             -- 用户ID
    fd_dept_id AS group_id        -- 部门ID作为组织ID
FROM 
    ekp_org_person
WHERE 
    fd_is_business = 1
    AND fd_is_available = 1
    AND fd_dept_id IS NOT NULL;   -- 确保有部门
GO
*/

-- 方案 C: 基于 EKP 的多维组织关系 (完整方案)
-- 如果您的系统支持一个用户属于多个部门/组织
/*
CREATE OR ALTER VIEW vw_user_group_membership AS
SELECT DISTINCT
    u.fd_id AS user_id,
    o.fd_id AS group_id
FROM 
    ekp_org_person u
    LEFT JOIN ekp_org_post_person pp ON pp.fd_person_id = u.fd_id  -- 岗位关系
    LEFT JOIN ekp_org_post p ON p.fd_id = pp.fd_post_id            -- 岗位
    LEFT JOIN ekp_org_org o ON o.fd_id = COALESCE(p.fd_org_id, u.fd_dept_id)  -- 组织(来自岗位或主部门)
WHERE 
    u.fd_is_business = 1
    AND u.fd_is_available = 1
    AND o.fd_is_available = 1
    AND o.fd_id IS NOT NULL;
GO
*/

-- =====================================================
-- 验证视图是否正确创建
-- =====================================================

-- 1. 检查视图结构
SELECT TOP 1 * FROM vw_user_group_membership;

-- 2. 统计每个用户的组织数量
SELECT 
    user_id,
    COUNT(*) AS org_count
FROM 
    vw_user_group_membership
GROUP BY 
    user_id
ORDER BY 
    org_count DESC;

-- 3. 检查是否有无效的组织ID (不在组织视图中)
SELECT DISTINCT
    m.group_id,
    o.display_name
FROM 
    vw_user_group_membership m
    LEFT JOIN vw_org_structure_sync o ON o.id = m.group_id
WHERE 
    o.id IS NULL;  -- 如果有结果,说明 group_id 与组织视图不匹配

-- 4. 统计总体数据量
SELECT 
    COUNT(DISTINCT user_id) AS total_users,
    COUNT(DISTINCT group_id) AS total_groups,
    COUNT(*) AS total_memberships
FROM 
    vw_user_group_membership;

-- =====================================================
-- 环境变量配置提醒
-- =====================================================
-- 
-- 创建视图后,请在运行同步脚本之前设置环境变量:
-- 
-- PowerShell:
--   $env:EKP_USER_GROUP_VIEW = "vw_user_group_membership"
-- 
-- 或者在 run-sync.ps1 中添加:
--   $env:EKP_USER_GROUP_VIEW = "vw_user_group_membership"
-- 
-- 如果不设置此变量,程序将回退使用用户的 dept_id 作为唯一组织
-- =====================================================
