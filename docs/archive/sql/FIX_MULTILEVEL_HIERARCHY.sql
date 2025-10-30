-- =====================================================
-- 修复 vw_org_structure_sync 视图：正确支持多层级部门嵌套
-- =====================================================
-- 问题：视图递归条件错误，导致只返回 2 层（0层公司 + 1层部门）
-- 原因：递归部分限制了 e.fd_org_type = 2，应该允许部门下再挂部门
-- 解决：移除类型限制，让递归正确获取所有子部门

USE [ekp];
GO

PRINT '修复 vw_org_structure_sync 视图 - 支持完整多层级部门...';
GO

ALTER VIEW [dbo].[vw_org_structure_sync] AS
WITH 
-- 1. 定义目标公司
target_companies AS (
    SELECT fd_id AS company_id
    FROM dbo.sys_org_element
    WHERE fd_is_available = 1
      AND fd_org_type = 1  -- 公司
      AND fd_id IN ('16f1c1a4910426f41649fd14862b99a1', '18e389224b660b4d67413f8466285581')
),
-- 2. 递归获取目标公司下的所有组织（包括多层部门）
org_hierarchy AS (
    -- 锚点：目标公司本身
    SELECT 
        e.fd_id AS id,
        e.fd_name AS display_name,
        e.fd_org_type AS org_type,
        CAST(NULL AS nvarchar(255)) AS parent_id,
        CAST(N'fzswjtOrganization' AS nvarchar(100)) AS owner,
        e.fd_create_time AS created_time,
        e.fd_alter_time AS updated_time,
        e.fd_is_available AS is_enabled,
        0 AS depth
    FROM dbo.sys_org_element e
    INNER JOIN target_companies tc ON e.fd_id = tc.company_id
    WHERE e.fd_is_available = 1
    
    UNION ALL
    
    -- 递归：获取所有子组织（不限制类型，允许部门下再挂部门）
    SELECT 
        e.fd_id,
        e.fd_name,
        e.fd_org_type,
        -- 优先使用 fd_parentid（直接父级），fd_parentorgid 通常指向顶级公司
        CAST(COALESCE(e.fd_parentid, e.fd_parentorgid) AS nvarchar(255)) AS parent_id,
        CAST(N'fzswjtOrganization' AS nvarchar(100)),
        e.fd_create_time,
        e.fd_alter_time,
        e.fd_is_available,
        oh.depth + 1
    FROM dbo.sys_org_element e
    INNER JOIN org_hierarchy oh ON (e.fd_parentid = oh.id OR e.fd_parentorgid = oh.id)
    WHERE e.fd_is_available = 1
      AND e.fd_org_type IN (1, 2)  -- 允许公司和部门类型（部门下可以再挂部门）
      AND oh.depth < 10  -- 防止循环，最多10层
)
SELECT 
    id,
    id AS name,
    display_name,
    parent_id,
    CASE WHEN org_type = 1 THEN 'company' ELSE 'department' END AS type,
    owner,
    created_time,
    updated_time,
    CAST(NULL AS NVARCHAR(255)) AS dept_id,
    CAST(CASE WHEN is_enabled = 1 THEN 1 ELSE 0 END AS BIT) AS is_enabled
FROM org_hierarchy
WHERE is_enabled = 1;
GO

PRINT '✓ vw_org_structure_sync 视图修复完成';
PRINT '';
GO

-- 验证修复效果
PRINT '验证组织层级分布:';
GO

WITH depth_calc AS (
    SELECT 
        v.id,
        v.display_name,
        v.parent_id,
        CASE 
            WHEN v.parent_id IS NULL THEN 0
            WHEN v.parent_id IN (SELECT id FROM dbo.vw_org_structure_sync WHERE parent_id IS NULL) THEN 1
            WHEN v.parent_id IN (
                SELECT id FROM dbo.vw_org_structure_sync 
                WHERE parent_id IN (SELECT id FROM dbo.vw_org_structure_sync WHERE parent_id IS NULL)
            ) THEN 2
            WHEN v.parent_id IN (
                SELECT id FROM dbo.vw_org_structure_sync v2
                WHERE v2.parent_id IN (
                    SELECT id FROM dbo.vw_org_structure_sync v3
                    WHERE v3.parent_id IN (SELECT id FROM dbo.vw_org_structure_sync WHERE parent_id IS NULL)
                )
            ) THEN 3
            ELSE 4
        END AS depth_level
    FROM dbo.vw_org_structure_sync v
)
SELECT 
    depth_level AS [层级],
    COUNT(*) AS [组织数量]
FROM depth_calc
GROUP BY depth_level
ORDER BY depth_level;
GO

PRINT '';
PRINT '视图修复完成！现在应该能看到完整的多层级组织结构。';
PRINT '请重新运行同步程序测试。';
GO
